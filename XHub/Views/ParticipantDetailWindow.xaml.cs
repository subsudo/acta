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
    private readonly NavigatorWordService _wordService = new();
    private readonly InitialsResolver _initialsResolver = new();
    private ParticipantIndexEntry? _participant;
    private IReadOnlyList<DetailModuleConfig> _modules = ListRepository.NormalizeModules(null);

    public ParticipantDetailWindow() => InitializeComponent();

    public void UpdateParticipant(ParticipantIndexEntry? participant, IReadOnlyList<DetailModuleConfig> modules)
    {
        _participant = participant;
        _modules = modules;
        DetailTitleTextBlock.Text = participant?.DisplayName ?? "Keine Auswahl";
        InitialsTextBlock.Text = participant?.Initials ?? string.Empty;
        Title = participant is null ? "Teilnehmerdetails" : $"{participant.DisplayName} - Acta";
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
                QuickActionKeys.Bu => CreateActionButton("BU", (_, _) => OpenBookmark(_participant, App.Config.WordBuBookmarkName), HasDocumentOrFolder(_participant) && _wordService.IsWordAvailable),
                QuickActionKeys.Bi => CreateActionButton("BI", (_, _) => OpenBookmark(_participant, App.Config.WordBiBookmarkName), HasDocumentOrFolder(_participant) && _wordService.IsWordAvailable),
                QuickActionKeys.Be => CreateActionButton("BE", (_, _) => OpenBookmark(_participant, App.Config.WordBeBookmarkName), HasDocumentOrFolder(_participant) && _wordService.IsWordAvailable),
                QuickActionKeys.Lb => CreateActionButton("LB", (_, _) => OpenBookmark(_participant, App.Config.WordLbBookmarkName), HasDocumentOrFolder(_participant) && _wordService.IsWordAvailable),
                QuickActionKeys.EntryBu => CreateActionButton("E BU", (_, _) => OpenBookmark(_participant, App.Config.WordEntryBuBookmarkName), HasDocumentOrFolder(_participant) && _wordService.IsWordAvailable),
                QuickActionKeys.EntryBi => CreateActionButton("E BI", (_, _) => OpenBookmark(_participant, App.Config.WordEntryBiBookmarkName), HasDocumentOrFolder(_participant) && _wordService.IsWordAvailable),
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

    private Button CreateActionButton(string label, RoutedEventHandler handler, bool isEnabled)
    {
        var button = new Button { Content = label, Style = (Style)FindResource("SecondaryButtonStyle"), IsEnabled = isEnabled };
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
    private void OpenAkte(ParticipantIndexEntry entry) => TryWordAction(entry, path => _wordService.OpenDocument(path));
    private void OpenBookmark(ParticipantIndexEntry entry, string bookmark) => TryWordAction(entry, path => _wordService.OpenDocumentAtBookmark(path, bookmark));

    private void TryWordAction(ParticipantIndexEntry entry, Action<string> action)
    {
        if (!WordBusyGuard.TryEnter())
        {
            return;
        }

        var previousCursor = Mouse.OverrideCursor;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            action(ResolveDocumentPath(entry));
        }
        catch (Exception ex)
        {
            AppLogger.Error($"XHub.DetailWindow.WordAction '{entry.DisplayName}'", ex);
            if (NavigatorWordService.IsDocumentLockedMessage(ex.Message))
            {
                NoticeWindow.ShowNotice(
                    this,
                    "Akte momentan gesperrt",
                    ex.Message,
                    "Die Akte wird gerade verwendet oder ist im Moment schreibgeschuetzt erreichbar.");
                return;
            }

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
        var docPath = _wordService.FindVerlaufsakte(entry.FolderPath, App.Config.VerlaufsakteKeyword);
        entry.DocumentPath = docPath;
        entry.Initials = _initialsResolver.TryResolveFromDocumentPath(docPath);
        InitialsTextBlock.Text = entry.Initials;
        RebuildModules();
        return docPath;
    }

    private static bool HasDocumentOrFolder(ParticipantIndexEntry entry) => File.Exists(entry.DocumentPath) || Directory.Exists(entry.FolderPath);
}
