using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace XHub.Services;

public class WordService
{
    private const string DocumentLockedMessage = "Akte ist bereits offen oder gesperrt (evtl. durch anderen Benutzer). Bitte später erneut versuchen.";
    private const int WordForegroundRetryDelayMs = 80;
    public const int NativeOpenCooldownMs = 400;

    public static bool IsWordAvailable => Type.GetTypeFromProgID("Word.Application") is not null;

    public static string FindVerlaufsakte(string folderPath, string keyword)
    {
        var matches = FindVerlaufsakteCandidates(folderPath, keyword);
        if (matches.Count > 1)
        {
            throw new InvalidOperationException("Mehrere Verlaufsakten gefunden");
        }

        return matches[0];
    }

    public static List<string> FindVerlaufsakteCandidates(string folderPath, string keyword)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Ordner nicht gefunden: {folderPath}");
        }

        var files = Directory
            .GetFiles(folderPath, "*.docx", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            throw new InvalidOperationException("Keine Verlaufsakte gefunden");
        }

        return files;
    }

    public static void OpenDocumentViaShell(string docPath)
    {
        if (!File.Exists(docPath))
        {
            throw new FileNotFoundException("Dokumentdatei nicht gefunden", docPath);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = docPath,
                UseShellExecute = true
            });
            AppLogger.Info($"XHub.Word.NativeOpen gestartet. Doc='{docPath}'");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Die Akte konnte nicht an Word übergeben werden.", ex);
        }
    }

    public void OpenDocumentAtBookmark(string docPath, string bookmarkName)
    {
        AppLogger.Info($"XHub.Word.OpenDocumentAtBookmark start. Doc='{docPath}', Bookmark='{bookmarkName}'");
        if (!IsWordAvailable)
        {
            throw new InvalidOperationException("Microsoft Word wurde nicht gefunden");
        }

        if (!File.Exists(docPath))
        {
            throw new FileNotFoundException("Dokumentdatei nicht gefunden", docPath);
        }

        if (IsFileLocked(docPath))
        {
            AppLogger.Info($"XHub.Word.Lock-Precheck zu nativem Öffnen ohne Bookmark. Doc='{docPath}', Bookmark='{bookmarkName}'");
            OpenDocumentViaShell(docPath);
            Thread.Sleep(NativeOpenCooldownMs);
            return;
        }

        dynamic? app = null;
        dynamic? doc = null;
        var shouldQuitCreatedApp = false;
        var openedHere = false;
        var operationSucceeded = false;

        try
        {
            var wordApp = CreateOrAttachWordApplication();
            app = wordApp.App;
            shouldQuitCreatedApp = wordApp.WasCreatedHere;

            AppLogger.Info($"XHub.Word.Instance attached={!wordApp.WasCreatedHere}, initialUnsaved={wordApp.InitialUnsavedDocumentCount}");

            doc = OpenOrGetDocument(app, docPath, out openedHere);
            AppLogger.Info($"XHub.Word.Document openedHere={openedHere}. Doc='{docPath}'");

            CloseTransientEmptyDocuments(app, docPath, wordApp.InitialUnsavedDocumentCount);
            EnsureWordUiState(app);
            EnsureDocumentIsWritableForBookmark(doc);

            if (!doc.Bookmarks.Exists(bookmarkName))
            {
                throw new InvalidOperationException($"Bookmark '{bookmarkName}' nicht gefunden. Bitte Vorlage prüfen.");
            }

            FocusBookmarkAtTop(app, doc, bookmarkName);

            operationSucceeded = true;
            shouldQuitCreatedApp = false;
        }
        catch (DocumentLockedException ex)
        {
            AppLogger.Info($"XHub.Word.Lock-Fallback zu nativem Öffnen ohne Bookmark. Doc='{docPath}', Bookmark='{bookmarkName}', Message='{ex.Message}'");
            if (openedHere)
            {
                TryCloseDocument(doc);
                openedHere = false;
            }

            OpenDocumentViaShell(docPath);
            Thread.Sleep(NativeOpenCooldownMs);
            return;
        }
        finally
        {
            if (!operationSucceeded && openedHere && !shouldQuitCreatedApp)
            {
                TryCloseDocument(doc);
            }

            ReleaseComObject(doc);
            if (shouldQuitCreatedApp)
            {
                TryQuitWordApplication(app);
            }

            ReleaseComObject(app);
        }
    }

    private static WordApplicationHandle CreateOrAttachWordApplication()
    {
        try
        {
            var clsid = new Guid("000209FF-0000-0000-C000-000000000046");
            NativeMethods.GetActiveObject(ref clsid, IntPtr.Zero, out var runningApp);
            if (runningApp is not null)
            {
                var initialUnsaved = CountUnsavedDocuments(runningApp);
                return new WordApplicationHandle(runningApp, false, initialUnsaved);
            }
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x800401E3)
        {
        }

        var wordType = Type.GetTypeFromProgID("Word.Application");
        if (wordType is null)
        {
            throw new InvalidOperationException("Microsoft Word wurde nicht gefunden");
        }

        var app = Activator.CreateInstance(wordType);
        if (app is null)
        {
            throw new InvalidOperationException("Microsoft Word konnte nicht gestartet werden");
        }

        var startupUnsaved = CountUnsavedDocuments(app);
        if (startupUnsaved > 0)
        {
            AppLogger.Info($"XHub.Word: Neu gestartete Instanz brachte {startupUnsaved} ungespeicherte Dokument(e) mit; Baseline bleibt 0.");
        }

        return new WordApplicationHandle(app, true, 0);
    }

    private static dynamic OpenOrGetDocument(dynamic app, string docPath, out bool openedHere)
    {
        var targetPath = Path.GetFullPath(docPath);
        dynamic? docs = null;

        try
        {
            docs = app.Documents;
            var count = (int)docs.Count;
            for (var i = 1; i <= count; i++)
            {
                dynamic? openDoc = null;
                try
                {
                    openDoc = docs[i];
                    var openPath = TryGetDocumentFullPath(openDoc);
                    if (!string.IsNullOrWhiteSpace(openPath) &&
                        Path.GetFullPath(openPath).Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var isReadOnly = GetDocumentReadOnlyOrThrow(openDoc);
                        if (isReadOnly)
                        {
                            throw new DocumentLockedException(BuildDocumentLockedMessage());
                        }

                        openedHere = false;
                        var result = openDoc;
                        openDoc = null;
                        return result;
                    }
                }
                finally
                {
                    ReleaseComObject(openDoc);
                }
            }

            if (IsFileLocked(docPath))
            {
                throw new DocumentLockedException(BuildDocumentLockedMessage());
            }

            try
            {
                openedHere = true;
                return docs.Open(docPath, ReadOnly: false, AddToRecentFiles: false);
            }
            catch (COMException ex) when (IsLockRelatedHResult((uint)ex.HResult))
            {
                openedHere = false;
                throw new DocumentLockedException(BuildDocumentLockedMessage(), ex);
            }
        }
        finally
        {
            ReleaseComObject(docs);
        }
    }

    private static void EnsureDocumentIsWritableForBookmark(dynamic doc)
    {
        var isReadOnly = GetDocumentReadOnlyOrThrow(doc);

        if (!isReadOnly)
        {
            return;
        }

        throw new DocumentLockedException(BuildDocumentLockedMessage());
    }

    private static void EnsureWordUiState(dynamic app)
    {
        try
        {
            app.UserControl = true;
        }
        catch (COMException ex)
        {
            if (ex.Message.Contains("schreibgeschützte Eigenschaft", StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Info("XHub.Word.UserControl ist in dieser Word-Variante nicht beschreibbar.");
            }
            else
            {
                AppLogger.Warn($"XHub.Word.UserControl konnte nicht gesetzt werden: {ex.Message}");
            }
        }

        app.Visible = true;
        TryBringWordToForeground(app);
    }

    private static int CountUnsavedDocuments(dynamic app)
    {
        dynamic? docs = null;
        var count = 0;

        try
        {
            docs = app.Documents;
            var total = (int)docs.Count;
            for (var i = 1; i <= total; i++)
            {
                dynamic? openDoc = null;
                try
                {
                    openDoc = docs[i];
                    if (IsUnsavedDocument(openDoc))
                    {
                        count++;
                    }
                }
                finally
                {
                    ReleaseComObject(openDoc);
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word: Unsaved-Dokumente konnten nicht gezaehlt werden: {ex.Message}");
        }
        finally
        {
            ReleaseComObject(docs);
        }

        return count;
    }

    private static bool IsUnsavedDocument(dynamic doc)
    {
        try
        {
            var path = doc.Path as string;
            return string.IsNullOrWhiteSpace(path);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Unsaved-Status konnte nicht gelesen werden ({ex.GetType().Name}): {ex.Message}");
            return false;
        }
    }

    private static void CloseTransientEmptyDocuments(dynamic app, string targetDocPath, int initialUnsavedDocumentCount)
    {
        dynamic? docs = null;
        var unsavedDocs = new List<dynamic>();
        var targetPath = Path.GetFullPath(targetDocPath);

        try
        {
            docs = app.Documents;
            for (var i = (int)docs.Count; i >= 1; i--)
            {
                dynamic? openDoc = null;
                try
                {
                    openDoc = docs[i];
                    var openPath = TryGetDocumentFullPath(openDoc);
                    if (!string.IsNullOrWhiteSpace(openPath) &&
                        Path.GetFullPath(openPath).Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        openDoc = null;
                        continue;
                    }

                    if (!IsUnsavedDocument(openDoc))
                    {
                        continue;
                    }

                    unsavedDocs.Add(openDoc);
                    openDoc = null;
                }
                finally
                {
                    ReleaseComObject(openDoc);
                }
            }

            var unsavedNow = unsavedDocs.Count;
            var documentsToClose = Math.Max(0, unsavedNow - initialUnsavedDocumentCount);
            AppLogger.Info($"XHub.Word: Unsaved before={initialUnsavedDocumentCount}, now={unsavedNow}, closing={documentsToClose}.");

            for (var i = 0; i < documentsToClose && i < unsavedDocs.Count; i++)
            {
                try
                {
                    unsavedDocs[i].Close(false);
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"XHub.Word: Transientes Leerdokument konnte nicht geschlossen werden: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word: Leerdokument-Pruefung fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            foreach (var unsavedDoc in unsavedDocs)
            {
                ReleaseComObject(unsavedDoc);
            }

            ReleaseComObject(docs);
        }
    }

    private static string? TryGetDocumentFullPath(dynamic doc)
    {
        try
        {
            return doc.FullName as string;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Doc.FullName konnte nicht gelesen werden ({ex.GetType().Name}): {ex.Message}");
            return null;
        }
    }

    private static void TryCloseDocument(dynamic? doc)
    {
        if (doc is null)
        {
            return;
        }

        try
        {
            doc.Close(false);
            AppLogger.Info("XHub.Word: Von XHub geoeffnetes Dokument im Fehlerfall wieder geschlossen.");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word: Dokument konnte im Fehlerfall nicht geschlossen werden: {ex.Message}");
        }
    }

    private static void FocusBookmarkAtTop(dynamic app, dynamic doc, string bookmarkName)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            dynamic? bookmarks = null;
            dynamic? bookmark = null;
            dynamic? targetRange = null;
            try
            {
                bookmarks = doc.Bookmarks;
                bookmark = bookmarks[bookmarkName];
                targetRange = bookmark.Range;
                FocusRangeAtTop(app, targetRange);
            }
            finally
            {
                ReleaseComObject(targetRange);
                ReleaseComObject(bookmark);
                ReleaseComObject(bookmarks);
            }

            if (attempt < 3)
            {
                Thread.Sleep(120);
            }
        }
    }

    private static void FocusRangeAtTop(dynamic app, dynamic range)
    {
        try
        {
            TryActivateWordWindow(app);
            app.Activate();
            var start = (int)range.Start;
            app.Selection.SetRange(start, start);
            app.ActiveWindow?.ScrollIntoView(app.Selection.Range, true);
            TryBringWordToForeground(app);
            return;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Range-Fokus fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }

        try
        {
            TryActivateWordWindow(app);
            range.Select();
            app.ActiveWindow?.ScrollIntoView(range, true);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Range.Select fallback fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }

        TryBringWordToForeground(app);
    }

    private static void FocusDocument(dynamic app, dynamic doc)
    {
        try
        {
            doc.Activate();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Doc.Activate fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }

        TryActivateWordWindow(app);
        TryBringWordToForeground(app);
    }

    private static void TryActivateWordWindow(dynamic app)
    {
        try
        {
            app.ActiveWindow?.Activate();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.ActiveWindow.Activate fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private static void TryBringWordToForeground(dynamic app)
    {
        var hwnd = TryGetWordMainWindowHandle(app);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            if (NativeMethods.IsIconic(hwnd))
            {
                NativeMethods.ShowWindowAsync(hwnd, NativeMethods.SW_RESTORE);
            }

            NativeMethods.SetForegroundWindow(hwnd);
            Thread.Sleep(WordForegroundRetryDelayMs);
            if (NativeMethods.IsIconic(hwnd))
            {
                NativeMethods.ShowWindowAsync(hwnd, NativeMethods.SW_RESTORE);
            }
            else
            {
                NativeMethods.ShowWindowAsync(hwnd, NativeMethods.SW_SHOW);
            }

            NativeMethods.SetForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Foreground fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private static IntPtr TryGetWordMainWindowHandle(dynamic app)
    {
        try
        {
            var hwndRaw = Convert.ToInt64(app.Hwnd);
            return hwndRaw > 0 ? new IntPtr(hwndRaw) : IntPtr.Zero;
        }
        catch (Exception ex)
        {
            if (ex.GetType().Name == "RuntimeBinderException")
            {
                AppLogger.Info("XHub.Word.Hwnd ist in dieser Word-Variante nicht direkt verfügbar.");
            }
            else
            {
                AppLogger.Warn($"XHub.Word.Hwnd konnte nicht gelesen werden ({ex.GetType().Name}): {ex.Message}");
            }

            return IntPtr.Zero;
        }
    }

    private static bool GetDocumentReadOnlyOrThrow(dynamic doc)
    {
        try
        {
            return (bool)doc.ReadOnly;
        }
        catch (COMException ex)
        {
            AppLogger.Warn($"XHub.Word.ReadOnly-Status konnte nicht gelesen werden ({ex.GetType().Name}): {ex.Message}");
            throw new InvalidOperationException("Der Schreibstatus der Akte konnte nicht geprüft werden. Bitte erneut versuchen.", ex);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.ReadOnly-Status konnte nicht gelesen werden ({ex.GetType().Name}): {ex.Message}");
            throw new InvalidOperationException("Der Schreibstatus der Akte konnte nicht geprüft werden. Bitte erneut versuchen.", ex);
        }
    }

    private static bool IsFileLocked(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException ex) when ((uint)ex.HResult == 0x80070020 || (uint)ex.HResult == 0x80070021)
        {
            return true;
        }
    }

    private static bool IsLockRelatedHResult(uint hresult)
    {
        return hresult is 0x80070020
            or 0x80070021
            or 0x800A175D;
    }

    private static string BuildDocumentLockedMessage()
    {
        var message = DocumentLockedMessage;
        try
        {
            var localWordProcessCount = Process.GetProcessesByName("WINWORD").Length;
            if (localWordProcessCount > 1)
            {
                message += " Hinweis: Mehrere lokale Word-Instanzen erkannt.";
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.WINWORD-Prozesszahl konnte nicht gelesen werden ({ex.GetType().Name}): {ex.Message}");
        }

        return message;
    }

    private static void TryQuitWordApplication(dynamic? app)
    {
        if (app is null)
        {
            return;
        }

        try
        {
            app.Quit(false);
            AppLogger.Info("XHub.Word: Selbst gestartete Word-Instanz wurde beendet.");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word: Selbst gestartete Instanz konnte nicht beendet werden: {ex.Message}");
        }
    }

    private static void ReleaseComObject(dynamic? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    private static class NativeMethods
    {
        public const int SW_SHOW = 5;
        public const int SW_RESTORE = 9;

        [DllImport("oleaut32.dll", PreserveSig = false)]
        public static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);
    }

    private sealed class WordApplicationHandle
    {
        public WordApplicationHandle(dynamic app, bool wasCreatedHere, int initialUnsavedDocumentCount)
        {
            App = app;
            WasCreatedHere = wasCreatedHere;
            InitialUnsavedDocumentCount = initialUnsavedDocumentCount;
        }

        public dynamic App { get; }
        public bool WasCreatedHere { get; }
        public int InitialUnsavedDocumentCount { get; }
    }
}

public sealed class DocumentLockedException : InvalidOperationException
{
    public DocumentLockedException(string message)
        : base(message)
    {
    }

    public DocumentLockedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
