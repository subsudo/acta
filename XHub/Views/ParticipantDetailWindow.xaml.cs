using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using XHub.Models;
using XHub.Services;

namespace XHub.Views;

public partial class ParticipantDetailWindow : Window
{
    private readonly InitialsResolver _initialsResolver = new();
    private readonly ParticipantHintsService _participantHintsService = new(App.Config.ParticipantHintsStorePath);
    private ParticipantIndexEntry? _participant;
    private IReadOnlyList<DetailModuleConfig> _modules = ListRepository.NormalizeModules(null);

    public ParticipantDetailWindow()
    {
        InitializeComponent();
        WordBusyGuard.BusyStateChanged += WordBusyGuard_OnBusyStateChanged;
        Closed += ParticipantDetailWindow_OnClosed;
    }

    public void UpdateParticipant(ParticipantIndexEntry? participant, IReadOnlyList<DetailModuleConfig> modules)
    {
        _participant = participant;
        _modules = modules;
        DetailTitleTextBlock.Text = participant?.DisplayName ?? "Keine Auswahl";
        InitialsTextBlock.Text = participant?.Initials ?? string.Empty;
        Title = participant is null ? "Teilnehmerdetails" : $"{participant.DisplayName} - Acta";
        if (participant is not null)
        {
            RefreshParticipantHintsForParticipant(participant);
        }
        RebuildActions();
        RebuildModules();
    }

    private void RebuildActions()
    {
        QuickActionPanel.Children.Clear();
        if (_participant is null || !_modules.Any(module => module.Key == DetailModuleKeys.Actions && module.IsEnabled))
        {
            return;
        }

        foreach (var actionKey in App.Config.VisibleQuickActions)
        {
            var button = actionKey switch
            {
                QuickActionKeys.Folder => CreateActionButton("Ordner", (_, _) => OpenFolder(_participant), Directory.Exists(_participant.FolderPath)),
                QuickActionKeys.Document => CreateActionButton("Akte", (_, _) => OpenAkte(_participant), HasDocumentOrFolder(_participant)),
                QuickActionKeys.Bu => CreateActionButton("BU", (_, _) => OpenBookmark(_participant, App.Config.WordBuBookmarkName), HasDocumentOrFolder(_participant) && WordService.IsWordAvailable),
                QuickActionKeys.Bi => CreateActionButton("BI", (_, _) => OpenBookmark(_participant, App.Config.WordBiBookmarkName), HasDocumentOrFolder(_participant) && WordService.IsWordAvailable),
                QuickActionKeys.Be => CreateActionButton("BE", (_, _) => OpenBookmark(_participant, App.Config.WordBeBookmarkName), HasDocumentOrFolder(_participant) && WordService.IsWordAvailable),
                QuickActionKeys.Lb => CreateActionButton("LB", (_, _) => OpenBookmark(_participant, App.Config.WordLbBookmarkName), HasDocumentOrFolder(_participant) && WordService.IsWordAvailable),
                QuickActionKeys.EntryBu => CreateActionButton("Eintrag BU", (_, _) => InsertStructuredEntry(_participant, StructuredEntryTarget.Bu), HasDocumentOrFolder(_participant) && WordService.IsWordAvailable),
                QuickActionKeys.EntryBi => CreateActionButton("Eintrag BI", (_, _) => InsertStructuredEntry(_participant, StructuredEntryTarget.Bi), HasDocumentOrFolder(_participant) && WordService.IsWordAvailable),
                QuickActionKeys.EntryBe => CreateActionButton("Eintrag BE", (_, _) => InsertStructuredEntry(_participant, StructuredEntryTarget.Be), HasDocumentOrFolder(_participant) && WordService.IsWordAvailable),
                QuickActionKeys.EntryLb => CreateActionButton("Eintrag LB", (_, _) => InsertStructuredEntry(_participant, StructuredEntryTarget.Lb), HasDocumentOrFolder(_participant) && WordService.IsWordAvailable),
                _ => null
            };
            if (button is not null) QuickActionPanel.Children.Add(button);
        }
    }

    private void RebuildModules()
    {
        DetailModuleHost.Children.Clear();
        if (_participant is null)
        {
            DetailModuleHost.Children.Add(new TextBlock { Text = "Wähle im Hauptfenster einen Teilnehmenden aus.", Foreground = (Brush)FindResource("Brush.SecondaryText"), TextWrapping = TextWrapping.Wrap });
            return;
        }

        foreach (var module in _modules.OrderBy(module => module.Order))
        {
            if (!module.IsEnabled || module.Key == DetailModuleKeys.Actions) continue;
            if (module.Key == DetailModuleKeys.Overview) DetailModuleHost.Children.Add(CreateOverviewModule(_participant));
            if (module.Key == DetailModuleKeys.Image) DetailModuleHost.Children.Add(CreateImageModule(_participant));
            if (module.Key == DetailModuleKeys.Initials) DetailModuleHost.Children.Add(CreateInitialsModule(_participant));
        }

        DetailModuleHost.Children.Add(CreateHintsModule(_participant));
    }

    private Border CreateOverviewModule(ParticipantIndexEntry entry)
    {
        var panel = new StackPanel();
        panel.Children.Add(CreateValueRow("Ordner", entry.FolderPath));
        panel.Children.Add(CreateValueRow("Akte", string.IsNullOrWhiteSpace(entry.DocumentPath) ? "Noch nicht gefunden" : entry.DocumentPath));
        return CreateModuleCard("Übersicht", panel);
    }

    private Border CreateImageModule(ParticipantIndexEntry entry)
    {
        var wrapper = new StackPanel();
        wrapper.Children.Add(new Border { Height = 150, CornerRadius = new CornerRadius(10), BorderBrush = (Brush)FindResource("Brush.Border"), BorderThickness = new Thickness(1), Background = (Brush)FindResource("Brush.PanelBg"), Child = new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Initials) ? entry.DisplayName[..Math.Min(entry.DisplayName.Length, 1)] : entry.Initials, FontSize = 38, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("Brush.SecondaryText") } });
        wrapper.Children.Add(new TextBlock { Text = "Bildquelle ist vorbereitet und kann später produktiv angebunden werden.", Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("Brush.SecondaryText") });
        return CreateModuleCard("Bild", wrapper);
    }

    private Border CreateInitialsModule(ParticipantIndexEntry entry)
    {
        var wrapper = new StackPanel();
        wrapper.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(entry.Initials) ? "Kein Kürzel erkannt" : entry.Initials, FontSize = 26, FontWeight = FontWeights.Bold });
        wrapper.Children.Add(new TextBlock { Text = "Kürzel werden weiterhin ausschließlich aus dem Dateinamen der Akte gelesen.", Margin = new Thickness(0, 8, 0, 0), Foreground = (Brush)FindResource("Brush.SecondaryText"), TextWrapping = TextWrapping.Wrap });
        return CreateModuleCard("Kürzel", wrapper);
    }

    private Border CreateHintsModule(ParticipantIndexEntry entry)
    {
        var wrapper = new StackPanel();
        var hints = entry.ActiveHints;
        if (hints.Count == 0)
        {
            wrapper.Children.Add(new TextBlock { Text = "Keine aktiven Hinweise", Foreground = (Brush)FindResource("Brush.SecondaryText"), TextWrapping = TextWrapping.Wrap });
        }
        else
        {
            var panel = new WrapPanel();
            foreach (var hint in hints)
            {
                panel.Children.Add(CreateHintPill(hint));
            }

            wrapper.Children.Add(panel);
        }

        var editButton = new Button
        {
            Content = "Bearbeiten",
            Style = (Style)FindResource("SecondaryButtonStyle"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8, 0, 0)
        };
        editButton.Click += (_, _) => EditParticipantHints(entry);
        wrapper.Children.Add(editButton);

        return CreateModuleCard("Hinweise", wrapper);
    }

    private static Border CreateHintPill(ParticipantHintDisplay hint)
    {
        var content = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(hint.Code) ? hint.Value : $"{hint.Code} {hint.Value}",
            FontSize = 10,
            FontWeight = string.IsNullOrWhiteSpace(hint.Code) ? FontWeights.Normal : FontWeights.SemiBold,
            Foreground = CreateFrozenBrush(hint.PillForeground),
            TextWrapping = TextWrapping.Wrap
        };

        return new Border
        {
            Background = CreateFrozenBrush(hint.PillBackground),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 6),
            Child = content,
            ToolTip = hint.Text
        };
    }

    private Border CreateModuleCard(string title, UIElement content)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        stack.Children.Add(content);
        return new Border { Background = (Brush)FindResource("Brush.CardBg"), BorderBrush = (Brush)FindResource("Brush.Border"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8), Child = stack };
    }

    private TextBlock CreateValueRow(string label, string value)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        text.Inlines.Add(new Run($"{label}: ") { Foreground = (Brush)FindResource("Brush.SecondaryText") });
        text.Inlines.Add(new Run(string.IsNullOrWhiteSpace(value) ? "-" : value));
        return text;
    }

    private void RefreshParticipantHintsForParticipant(ParticipantIndexEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.DocumentPath) || !File.Exists(entry.DocumentPath))
        {
            try
            {
                entry.DocumentPath = ResolveDocumentPath(entry);
            }
            catch
            {
                entry.ActiveHints = Array.Empty<ParticipantHintDisplay>();
                return;
            }
        }

        try
        {
            entry.ActiveHints = _participantHintsService.LoadActiveDisplays(entry.DocumentPath);
        }
        catch (Exception ex)
        {
            entry.ActiveHints = Array.Empty<ParticipantHintDisplay>();
            AppLogger.Warn($"Hinweise konnten für '{entry.DisplayName}' nicht geladen werden: {ex.Message}");
        }
    }

    private void EditParticipantHints(ParticipantIndexEntry entry)
    {
        string documentPath;
        try
        {
            documentPath = ResolveDocumentPath(entry);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Für diesen Teilnehmer ist noch keine Akte verfügbar.\n\n{ex.Message}", "Hinweise nicht verfügbar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var session = _participantHintsService.LoadEditorSession(documentPath);
        if (!session.IsAvailable)
        {
            MessageBox.Show(session.ErrorMessage, "Hinweise nicht verfügbar", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new ParticipantHintsWindow(entry.DisplayName, session.Record.Hints)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var result = _participantHintsService.SaveEditorSession(session, dialog.GetAllItems());
        if (!result.Success)
        {
            MessageBox.Show(result.ErrorMessage, result.Conflict ? "Hinweise wurden geändert" : "Hinweise konnten nicht gespeichert werden", MessageBoxButton.OK, result.Conflict ? MessageBoxImage.Warning : MessageBoxImage.Error);
            return;
        }

        RefreshParticipantHintsForParticipant(entry);
        RebuildModules();
    }

    private static Brush CreateFrozenBrush(string hexColor)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
        brush.Freeze();
        return brush;
    }

    private Button CreateActionButton(string label, RoutedEventHandler handler, bool isEnabled)
    {
        var button = new Button
        {
            Content = label,
            Style = (Style)FindResource("SecondaryButtonStyle"),
            IsEnabled = isEnabled && !WordBusyGuard.IsBusy
        };
        button.Click += handler;
        return button;
    }

    private void OpenFolder(ParticipantIndexEntry entry)
    {
        if (!Directory.Exists(entry.FolderPath))
        {
            MessageBox.Show("Der Teilnehmerordner ist nicht erreichbar.", "Acta", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = entry.FolderPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLogger.Error($"XHub.DetailWindow.OpenFolder '{entry.FolderPath}'", ex);
            MessageBox.Show(
                $"Der Teilnehmerordner konnte nicht geöffnet werden:\n{ex.Message}",
                "Acta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
    private void OpenAkte(ParticipantIndexEntry entry) =>
        TryWordAction(entry, OpenDocumentViaShellWithCooldownAsync);

    private void OpenBookmark(ParticipantIndexEntry entry, string bookmark) =>
        TryWordAction(entry, path => App.WordStaHost.RunAsync(
            $"OpenDocumentAtBookmark:{bookmark}",
            service => service.OpenDocumentAtBookmark(path, bookmark)));

    private async void InsertStructuredEntry(ParticipantIndexEntry entry, StructuredEntryTarget target)
    {
        if (!WordBusyGuard.TryEnter())
        {
            return;
        }

        var previousCursor = Mouse.OverrideCursor;

        try
        {
            var documentPath = ResolveDocumentPath(entry);
            var fallbackFields = BuildFallbackEntryFieldsIfEnabled();
            var clipboardText = WordService.ReadClipboardTextWithRetry();

            Mouse.OverrideCursor = Cursors.AppStarting;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

            await App.WordStaHost.RunAsync(
                $"InsertStructuredEntry:{target.Key}",
                service => service.InsertClipboardToStructuredEntryTable(
                    documentPath,
                    target,
                    fallbackFields,
                    clipboardText,
                    bringToForeground: true));
        }
        catch (WordTemplateValidationException ex) when (ex.Kind == WordTemplateValidationErrorKind.BookmarkMissing)
        {
            MessageBox.Show(
                $"Der {target.Label} konnte nicht eingefügt werden.\n\nDie erwartete Textmarke '{ex.BookmarkName}' wurde in der Akte nicht gefunden.",
                "Acta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (WordTemplateValidationException ex) when (ex.Kind == WordTemplateValidationErrorKind.StructuredEntryTableInvalid)
        {
            MessageBox.Show(
                $"Der {target.Label} konnte nicht eingefügt werden.\n\n{ex.UserMessage}",
                "Acta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (DocumentLockedException ex)
        {
            MessageBox.Show(
                $"Die Akte von {entry.DisplayName} ist aktuell nicht schreibbar.\n\n{ex.Message}",
                "Acta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"XHub.DetailWindow.StructuredEntry '{entry.DisplayName}', Target='{target.Key}'", ex);
            MessageBox.Show(ex.Message, "Acta", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = previousCursor;
            WordBusyGuard.Exit();
        }
    }

    private static string[]? BuildFallbackEntryFieldsIfEnabled()
    {
        if (!App.UserPrefs.AutoPrefillOnEmptyClipboard)
        {
            return null;
        }

        var date = DateTime.Now.ToString("dd.MM.yy");
        var initials = (App.UserPrefs.DefaultEntryInitials ?? string.Empty).Trim();
        return new[] { date, initials, string.Empty, string.Empty };
    }

    private async void TryWordAction(ParticipantIndexEntry entry, Func<string, Task> action)
    {
        if (!WordBusyGuard.TryEnter())
        {
            return;
        }

        var previousCursor = Mouse.OverrideCursor;

        try
        {
            var documentPath = ResolveDocumentPath(entry);
            Mouse.OverrideCursor = Cursors.AppStarting;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            await action(documentPath);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"XHub.DetailWindow.WordAction '{entry.DisplayName}'", ex);
            MessageBox.Show(ex.Message, "Acta", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = previousCursor;
            WordBusyGuard.Exit();
        }
    }

    private string ResolveDocumentPath(ParticipantIndexEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.DocumentPath) && File.Exists(entry.DocumentPath)) return entry.DocumentPath;
        if (!Directory.Exists(entry.FolderPath)) throw new InvalidOperationException("Teilnehmerordner ist nicht erreichbar.");
        var docPath = WordService.FindVerlaufsakte(entry.FolderPath, App.Config.VerlaufsakteKeyword);
        entry.DocumentPath = docPath;
        entry.Initials = _initialsResolver.TryResolveFromDocumentPath(docPath);
        InitialsTextBlock.Text = entry.Initials;
        RebuildModules();
        return docPath;
    }

    private static bool HasDocumentOrFolder(ParticipantIndexEntry entry) => File.Exists(entry.DocumentPath) || Directory.Exists(entry.FolderPath);

    private static async Task OpenDocumentViaShellWithCooldownAsync(string documentPath)
    {
        WordService.OpenDocumentViaShell(documentPath);
        await Task.Delay(WordService.NativeOpenCooldownMs);
    }

    private void WordBusyGuard_OnBusyStateChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(WordBusyGuard_OnBusyStateChanged, sender, e);
            return;
        }

        RebuildActions();
    }

    private void ParticipantDetailWindow_OnClosed(object? sender, EventArgs e)
    {
        WordBusyGuard.BusyStateChanged -= WordBusyGuard_OnBusyStateChanged;
    }
}
