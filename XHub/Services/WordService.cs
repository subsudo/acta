using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;

namespace XHub.Services;

public class WordService
{
    private const string DocumentLockedMessage = "Akte ist bereits offen oder gesperrt (evtl. durch anderen Benutzer). Bitte später erneut versuchen.";
    private const int WordForegroundRetryDelayMs = 80;
    private const int ClipboardReadRetryCount = 3;
    private const int ClipboardReadRetryBaseDelayMs = 60;
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

    public static string ReadClipboardTextWithRetry()
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= ClipboardReadRetryCount; attempt++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch (COMException ex)
            {
                lastException = ex;
                AppLogger.Warn($"XHub.Word.Clipboard COM-Zugriff fehlgeschlagen (Attempt {attempt}/{ClipboardReadRetryCount}): {ex.Message}");
            }
            catch (ExternalException ex)
            {
                lastException = ex;
                AppLogger.Warn($"XHub.Word.Clipboard Zugriff fehlgeschlagen (Attempt {attempt}/{ClipboardReadRetryCount}): {ex.Message}");
            }

            if (attempt < ClipboardReadRetryCount)
            {
                Thread.Sleep(ClipboardReadRetryBaseDelayMs * (1 << (attempt - 1)));
            }
        }

        throw new InvalidOperationException(
            "Zwischenablage ist momentan blockiert. Bitte kurz warten und erneut versuchen.",
            lastException);
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

    public bool InsertClipboardToStructuredEntryTable(
        string docPath,
        StructuredEntryTarget target,
        string[]? fallbackFieldsWhenClipboardInvalid,
        string? preReadClipboardText,
        bool bringToForeground = true)
    {
        ArgumentNullException.ThrowIfNull(target);
        AppLogger.Info($"XHub.Word.InsertStructuredEntry start. Doc='{docPath}', Target='{target.Key}', Bookmark='{target.TableBookmarkName}'");

        if (!IsWordAvailable)
        {
            throw new InvalidOperationException("Microsoft Word wurde nicht gefunden");
        }

        if (!File.Exists(docPath))
        {
            throw new FileNotFoundException("Dokumentdatei nicht gefunden", docPath);
        }

        if (target.FirstDataRowIndex < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(target), "FirstDataRowIndex muss >= 2 sein.");
        }

        if (IsFileLocked(docPath))
        {
            throw new DocumentLockedException(BuildDocumentLockedMessage());
        }

        dynamic? app = null;
        dynamic? doc = null;
        dynamic? targetTable = null;
        var shouldQuitCreatedApp = false;
        var openedHere = false;
        var operationSucceeded = false;

        try
        {
            var wordApp = CreateOrAttachWordApplication();
            app = wordApp.App;
            shouldQuitCreatedApp = wordApp.WasCreatedHere;

            AppLogger.Info($"XHub.Word.Entry instance attached={!wordApp.WasCreatedHere}, initialUnsaved={wordApp.InitialUnsavedDocumentCount}");

            doc = OpenOrGetDocument(app, docPath, out openedHere);
            AppLogger.Info($"XHub.Word.Entry document openedHere={openedHere}. Doc='{docPath}'");

            CloseTransientEmptyDocuments(app, docPath, wordApp.InitialUnsavedDocumentCount);
            EnsureWordVisibleForEntry(app);
            EnsureDocumentIsWritableForBookmark(doc);

            targetTable = ResolveStructuredEntryTableForWrite(doc, target);
            var clipboardText = preReadClipboardText ?? string.Empty;
            var hasClipboardContent = !string.IsNullOrWhiteSpace(clipboardText);
            string[] clipboardFields = Array.Empty<string>();
            var hasValidClipboardRow = hasClipboardContent &&
                                       TryParseClipboardFields(clipboardText, target.ExpectedColumnCount, out clipboardFields);
            if (hasClipboardContent && !hasValidClipboardRow)
            {
                AppLogger.Warn($"XHub.Word.Entry: Clipboard-Format ungueltig fuer Target='{target.Key}', leere/vorbereitete Zeile wird eingefuegt.");
            }

            var hasFallbackFields = fallbackFieldsWhenClipboardInvalid is not null &&
                                    fallbackFieldsWhenClipboardInvalid.Length == target.ExpectedColumnCount;
            if (fallbackFieldsWhenClipboardInvalid is not null && !hasFallbackFields)
            {
                AppLogger.Warn($"XHub.Word.Entry: fallbackFields hat {fallbackFieldsWhenClipboardInvalid.Length} statt {target.ExpectedColumnCount} Spalten und wird ignoriert.");
            }

            dynamic? insertedRow = null;
            try
            {
                insertedRow = InsertRowAtTopOfDataArea(targetTable, target.FirstDataRowIndex);
                var rowIndex = (int)insertedRow.Index;
                var fields = hasValidClipboardRow
                    ? clipboardFields
                    : hasFallbackFields
                        ? fallbackFieldsWhenClipboardInvalid!
                        : Array.Empty<string>();

                for (var column = 1; column <= target.ExpectedColumnCount; column++)
                {
                    var value = column <= fields.Length ? fields[column - 1] ?? string.Empty : string.Empty;
                    SetTableCellText(targetTable, rowIndex, column, value);
                }
            }
            catch
            {
                TryDeleteRow(insertedRow);
                throw;
            }

            var preferredEditColumn = hasValidClipboardRow
                ? target.ValidClipboardFocusColumn
                : target.FallbackFocusColumn;
            var editColumn = GetSafeEditColumn(targetTable, preferredEditColumn);
            dynamic? editCell = null;
            dynamic? editRange = null;
            try
            {
                editCell = targetTable.Cell((int)insertedRow!.Index, editColumn);
                editRange = editCell.Range;
                FocusRangeAtTop(app, editRange, bringToForeground && target.BringToForeground, docPath);
            }
            finally
            {
                ReleaseComObject(editRange);
                ReleaseComObject(editCell);
                ReleaseComObject(targetTable);
                targetTable = null;
            }

            operationSucceeded = true;
            shouldQuitCreatedApp = false;
            AppLogger.Info($"XHub.Word.InsertStructuredEntry ok. Doc='{docPath}', Target='{target.Key}', ClipboardUsed={hasValidClipboardRow}, FocusColumn={editColumn}");
            return hasValidClipboardRow;
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x80040154)
        {
            AppLogger.Error("XHub.Word.InsertStructuredEntry COM Class not registered", ex);
            throw new InvalidOperationException("Microsoft Word wurde nicht gefunden");
        }
        catch (COMException ex) when (IsLockRelatedHResult((uint)ex.HResult))
        {
            AppLogger.Info($"XHub.Word.InsertStructuredEntry Lock-COM-Fehler. Doc='{docPath}', Target='{target.Key}', Message='{ex.Message}'");
            throw new DocumentLockedException(BuildDocumentLockedMessage(), ex);
        }
        catch (COMException ex)
        {
            AppLogger.Error("XHub.Word.InsertStructuredEntry COM Fehler", ex);
            throw new InvalidOperationException($"Fehler beim Zugriff auf Word: {ex.Message}", ex);
        }
        finally
        {
            ReleaseComObject(targetTable);
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

    private static void EnsureWordVisibleForEntry(dynamic app)
    {
        try
        {
            app.UserControl = true;
        }
        catch (COMException ex)
        {
            if (ex.Message.Contains("schreibgeschützte Eigenschaft", StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Info("XHub.Word.Entry.UserControl ist in dieser Word-Variante nicht beschreibbar.");
            }
            else
            {
                AppLogger.Warn($"XHub.Word.Entry.UserControl konnte nicht gesetzt werden: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Entry.UserControl konnte nicht gesetzt werden ({ex.GetType().Name}): {ex.Message}");
        }

        try
        {
            var isVisible = false;
            try
            {
                isVisible = (bool)app.Visible;
            }
            catch (Exception ex)
            {
                AppLogger.Info($"XHub.Word.Entry.Visible-Status konnte nicht gelesen werden ({ex.GetType().Name}): {ex.Message}");
            }

            if (!isVisible)
            {
                app.Visible = true;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Entry.Visible konnte nicht gesetzt werden ({ex.GetType().Name}): {ex.Message}");
        }
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

    private static void FocusRangeAtTop(dynamic app, dynamic range, bool bringToForeground, string docPath)
    {
        try
        {
            TryActivateWordWindow(app);
            app.Activate();
            var start = (int)range.Start;
            app.Selection.SetRange(start, start);
            app.ActiveWindow?.ScrollIntoView(app.Selection.Range, true);
            if (bringToForeground)
            {
                TryBringTargetWordWindowToForeground(docPath);
            }

            return;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Entry.Range-Fokus fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }

        try
        {
            TryActivateWordWindow(app);
            range.Select();
            app.ActiveWindow?.ScrollIntoView(range, true);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Entry.Range.Select fallback fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }

        if (bringToForeground)
        {
            TryBringTargetWordWindowToForeground(docPath);
        }
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

    private static void TryBringTargetWordWindowToForeground(string docPath)
    {
        try
        {
            var processIds = Process
                .GetProcessesByName("WINWORD")
                .Select(process => process.Id)
                .ToHashSet();
            if (processIds.Count == 0)
            {
                AppLogger.Info("XHub.Word.Entry.Foreground: Keine WINWORD-Prozesse gefunden.");
                return;
            }

            var windows = SnapshotWordTopLevelWindows(processIds)
                .Where(window => NativeMethods.IsWindowVisible(window.Hwnd))
                .ToArray();
            var match = TryFindTargetWordWindow(windows, docPath);
            if (match is null)
            {
                return;
            }

            TryForceForegroundWindow(match.Value.Hwnd, match.Value.ProcessId);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Entry.Foreground fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private static IReadOnlyList<WordWindowSnapshot> SnapshotWordTopLevelWindows(IReadOnlySet<int> processIds)
    {
        var windows = new List<WordWindowSnapshot>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0 || !processIds.Contains((int)processId))
            {
                return true;
            }

            var title = ReadWindowTitle(hwnd);
            if (!string.IsNullOrWhiteSpace(title))
            {
                windows.Add(new WordWindowSnapshot(hwnd, (int)processId, title));
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static WordWindowSnapshot? TryFindTargetWordWindow(IReadOnlyList<WordWindowSnapshot> windows, string docPath)
    {
        var fileName = Path.GetFileName(docPath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(docPath);
        var fullNameMatches = FindWordWindowTitleMatches(windows, fileName);
        if (fullNameMatches.Count == 1)
        {
            AppLogger.Info($"XHub.Word.Entry.Foreground: Fenster ueber Dateiname gefunden. Title='{fullNameMatches[0].Title}'");
            return fullNameMatches[0];
        }

        var stemMatches = FindWordWindowTitleMatches(windows, fileNameWithoutExtension);
        if (stemMatches.Count == 1)
        {
            AppLogger.Info($"XHub.Word.Entry.Foreground: Fenster ueber Dateistamm gefunden. Title='{stemMatches[0].Title}'");
            return stemMatches[0];
        }

        var candidateCount = fullNameMatches.Count > 0 ? fullNameMatches.Count : stemMatches.Count;
        AppLogger.Info(candidateCount == 0
            ? $"XHub.Word.Entry.Foreground: Kein eindeutiges Word-Fenster fuer '{docPath}' gefunden. WindowCount={windows.Count}."
            : $"XHub.Word.Entry.Foreground: Mehrere Kandidaten fuer '{docPath}' gefunden. CandidateCount={candidateCount}; Fokus wird nicht erzwungen.");
        return null;
    }

    private static IReadOnlyList<WordWindowSnapshot> FindWordWindowTitleMatches(
        IReadOnlyList<WordWindowSnapshot> windows,
        string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return Array.Empty<WordWindowSnapshot>();
        }

        return windows
            .Where(window => window.Title.Contains(alias.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static void TryForceForegroundWindow(IntPtr hwnd, int processId)
    {
        try
        {
            if (NativeMethods.IsIconic(hwnd))
            {
                NativeMethods.ShowWindowAsync(hwnd, NativeMethods.SW_RESTORE);
            }

            NativeMethods.AllowSetForegroundWindow((uint)processId);
            var foregroundSet = NativeMethods.SetForegroundWindow(hwnd);
            Thread.Sleep(WordForegroundRetryDelayMs);
            if (!foregroundSet)
            {
                foregroundSet = TrySetForegroundWindowWithThreadInput(hwnd);
            }

            if (!foregroundSet)
            {
                AppLogger.Info($"XHub.Word.Entry.Foreground: SetForegroundWindow wurde nicht akzeptiert. Hwnd={hwnd}, Pid={processId}.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Entry.Foreground-Fallback fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private static bool TrySetForegroundWindowWithThreadInput(IntPtr hwnd)
    {
        uint targetThreadId = 0;
        uint foregroundThreadId = 0;
        var currentThreadId = NativeMethods.GetCurrentThreadId();
        var attachedTarget = false;
        var attachedForeground = false;

        try
        {
            targetThreadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
            var foregroundWindow = NativeMethods.GetForegroundWindow();
            if (foregroundWindow != IntPtr.Zero)
            {
                foregroundThreadId = NativeMethods.GetWindowThreadProcessId(foregroundWindow, out _);
            }

            if (targetThreadId != 0 && targetThreadId != currentThreadId)
            {
                attachedTarget = NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, true);
            }

            if (foregroundThreadId != 0 &&
                foregroundThreadId != currentThreadId &&
                foregroundThreadId != targetThreadId)
            {
                attachedForeground = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            if (NativeMethods.IsIconic(hwnd))
            {
                NativeMethods.ShowWindowAsync(hwnd, NativeMethods.SW_RESTORE);
            }
            else
            {
                NativeMethods.ShowWindowAsync(hwnd, NativeMethods.SW_SHOW);
            }

            return NativeMethods.SetForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            AppLogger.Info($"XHub.Word.Entry.Foreground AttachThreadInput fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
            return false;
        }
        finally
        {
            if (attachedForeground)
            {
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }

            if (attachedTarget)
            {
                NativeMethods.AttachThreadInput(currentThreadId, targetThreadId, false);
            }
        }
    }

    private static string ReadWindowTitle(IntPtr hwnd)
    {
        try
        {
            var length = NativeMethods.GetWindowTextLength(hwnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(length + 1);
            _ = NativeMethods.GetWindowText(hwnd, builder, builder.Capacity);
            return builder.ToString();
        }
        catch
        {
            return string.Empty;
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

    private static bool TryParseClipboardFields(string clipboardText, int expectedColumnCount, out string[] fields)
    {
        fields = Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return false;
        }

        var normalized = clipboardText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim('\n');
        var lines = normalized.Split('\n', StringSplitOptions.None);
        if (lines.Length != 1)
        {
            return false;
        }

        var parts = lines[0].Split('\t');
        if (parts.Length != expectedColumnCount)
        {
            return false;
        }

        fields = parts;
        return true;
    }

    private static dynamic ResolveStructuredEntryTableForWrite(dynamic doc, StructuredEntryTarget target)
    {
        var bookmarkName = target.TableBookmarkName;
        if (!doc.Bookmarks.Exists(bookmarkName))
        {
            throw CreateBookmarkMissingException(bookmarkName);
        }

        dynamic? bookmark = null;
        dynamic? bookmarkRange = null;
        int bookmarkStart;
        try
        {
            bookmark = doc.Bookmarks[bookmarkName];
            bookmarkRange = bookmark.Range;
            bookmarkStart = (int)bookmarkRange.Start;

            var currentTable = GetContainingBookmarkTable(bookmarkRange);
            var returnedCurrentTable = false;
            try
            {
                if (currentTable is not null && IsStructuredEntryTable(currentTable, target.ExpectedColumnCount))
                {
                    returnedCurrentTable = true;
                    return currentTable!;
                }
            }
            finally
            {
                if (!returnedCurrentTable)
                {
                    ReleaseComObject(currentTable);
                }
            }
        }
        finally
        {
            ReleaseComObject(bookmarkRange);
            ReleaseComObject(bookmark);
        }

        var tableCount = (int)doc.Tables.Count;
        for (var tableIndex = 1; tableIndex <= tableCount; tableIndex++)
        {
            dynamic? table = null;
            try
            {
                table = doc.Tables[tableIndex];
                var tableStart = GetTableStart(table);
                if (tableStart < bookmarkStart)
                {
                    continue;
                }

                if (IsStructuredEntryTable(table, target.ExpectedColumnCount))
                {
                    var result = table;
                    table = null;
                    return result;
                }
            }
            finally
            {
                ReleaseComObject(table);
            }
        }

        throw CreateStructuredEntryTableInvalidException(bookmarkName, target.Key);
    }

    private static object? GetContainingBookmarkTable(dynamic bookmarkRange)
    {
        if ((int)bookmarkRange.Tables.Count <= 0)
        {
            return null;
        }

        return bookmarkRange.Tables[1];
    }

    private static bool IsStructuredEntryTable(dynamic table, int expectedColumnCount)
    {
        try
        {
            if ((int)table.Rows.Count < 1 || (int)table.Columns.Count != expectedColumnCount)
            {
                return false;
            }

            var expectedHeaders = new[] { "datum", "eintrag von", "thematik", "beschreibung" };
            if (expectedHeaders.Length != expectedColumnCount)
            {
                return false;
            }

            for (var column = 1; column <= expectedHeaders.Length; column++)
            {
                dynamic? cell = null;
                dynamic? range = null;
                try
                {
                    cell = table.Cell(1, column);
                    range = cell.Range;
                    var text = NormalizeTableCellText((string)range.Text);
                    if (!string.Equals(text, expectedHeaders[column - 1], StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                finally
                {
                    ReleaseComObject(range);
                    ReleaseComObject(cell);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetTableStart(dynamic table)
    {
        dynamic? range = null;
        try
        {
            range = table.Range;
            return (int)range.Start;
        }
        catch
        {
            return int.MaxValue;
        }
        finally
        {
            ReleaseComObject(range);
        }
    }

    private static string NormalizeTableCellText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\a", " ", StringComparison.Ordinal)
            .Trim();

        return string.Join(" ", cleaned
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .ToLowerInvariant();
    }

    private static dynamic InsertRowAtTopOfDataArea(dynamic targetTable, int firstDataRowIndex)
    {
        var existingRowCount = (int)targetTable.Rows.Count;
        if (existingRowCount < firstDataRowIndex)
        {
            while ((int)targetTable.Rows.Count < firstDataRowIndex)
            {
                targetTable.Rows.Add();
            }

            return targetTable.Rows[firstDataRowIndex];
        }

        return targetTable.Rows.Add(targetTable.Rows[firstDataRowIndex]);
    }

    private static void SetTableCellText(dynamic table, int rowIndex, int columnIndex, string value)
    {
        dynamic? cell = null;
        dynamic? range = null;
        try
        {
            cell = table.Cell(rowIndex, columnIndex);
            range = cell.Range;
            range.Text = value;
        }
        finally
        {
            ReleaseComObject(range);
            ReleaseComObject(cell);
        }
    }

    private static void TryDeleteRow(dynamic? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            row.Delete();
            AppLogger.Info("XHub.Word.Entry: Teilweise befuellte Zeile nach Fehler entfernt.");
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub.Word.Entry: Zeile konnte nach Fehler nicht entfernt werden: {ex.Message}");
        }
    }

    private static int GetSafeEditColumn(dynamic targetTable, int preferredColumn)
    {
        try
        {
            var columnCount = (int)targetTable.Columns.Count;
            return columnCount <= 0 ? 1 : Math.Clamp(preferredColumn, 1, columnCount);
        }
        catch
        {
            return Math.Max(1, preferredColumn);
        }
    }

    private static WordTemplateValidationException CreateBookmarkMissingException(string bookmarkName)
    {
        return new WordTemplateValidationException(
            WordTemplateValidationErrorKind.BookmarkMissing,
            bookmarkName,
            $"Bookmark '{bookmarkName}' nicht gefunden. Bitte Vorlage prüfen.");
    }

    private static WordTemplateValidationException CreateStructuredEntryTableInvalidException(
        string bookmarkName,
        string targetKey)
    {
        return new WordTemplateValidationException(
            WordTemplateValidationErrorKind.StructuredEntryTableInvalid,
            bookmarkName,
            $"{targetKey}-Verlaufstabelle hat nicht das erwartete Format. Bitte Vorlage prüfen.");
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

    private readonly record struct WordWindowSnapshot(IntPtr Hwnd, int ProcessId, string Title);

    private static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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
        public static extern bool AllowSetForegroundWindow(uint dwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
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
