using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;
using XHub.Models;
using XHub.Services;
using XHub.Views;

namespace XHub;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ObservableCollection<SavedList> _savedLists = new();
    private readonly ObservableCollection<ParticipantIndexEntry> _currentParticipants = new();
    private readonly ObservableCollection<ParticipantIndexEntry> _searchResults = new();
    private readonly ListRepository _listRepository = new(App.ListsPath, App.ListsBackupPath);
    private readonly ExportService _exportService = new();
    private readonly ParticipantSearchService _searchService = new();
    private readonly ParticipantArchiveService _archiveService = new();
    private readonly InitialsResolver _initialsResolver = new();
    private readonly DocxHeaderMetadataService _headerMetadataService;
    private readonly WeeklyScheduleService _weeklyScheduleService;
    private readonly AppUpdateService _appUpdateService;

    private ParticipantIndexService _indexService;
    private AttendanceImportService _attendanceImportService;
    private DispatcherTimer? _refreshTimer;
    private IReadOnlyList<ParticipantIndexEntry> _mainIndexEntries = Array.Empty<ParticipantIndexEntry>();
    private IReadOnlyList<ParticipantIndexEntry> _archiveIndexEntries = Array.Empty<ParticipantIndexEntry>();
    private IReadOnlyList<ParticipantIndexEntry> _indexEntries = Array.Empty<ParticipantIndexEntry>();
    private IReadOnlyDictionary<string, ParticipantIndexEntry> _indexEntriesByParticipantKey = new Dictionary<string, ParticipantIndexEntry>(StringComparer.OrdinalIgnoreCase);
    private SavedList _temporaryList = CreateWorkingList();
    private SavedList _workingList = CreateWorkingList();
    private string? _loadedSavedListId;
    private string _workingListLabel = "Temporäre Liste";
    private ParticipantIndexEntry? _selectedParticipant;
    private bool _isRefreshing;
    private bool _isListPanelOpen = true;
    private bool _isDetailPanelOpen;
    private bool _isNotesPanelOpen;
    private double? _lastNotesPanelWidth;
    private string? _archiveRootPath;
    private string? _loadedArchiveRootPath;
    private bool _isArchiveAvailable;
    private bool _isArchiveSearchEnabled;
    private bool _isArchiveLoading;
    private bool _suppressSearchResultsUntilTyping;
    private DateTime? _lastRefreshAt;
    private bool _isUpdateShutdownRequested;

    private const double BaseCompactWindowMinWidth = 400;
    private const double BaseListPanelWindowMinWidth = 520;
    private const double BaseDetailPanelWindowMinWidth = 680;
    private const double BaseFullPanelsWindowMinWidth = 860;
    // Toolbar width is ~340 DIP; with the 12+12 panel padding the real column minimum is 364.
    private const double NotesPanelMinWidth = 364;
    private const double NotesPanelDefaultWidth = NotesPanelMinWidth;
    private const double NotesPanelWidthContribution = NotesPanelMinWidth + 6;

    private int CurrentUiScaleLevel => App.NormalizeUiScaleLevel(App.UserPrefs.UiScaleLevel);
    public bool IsWordActionRunning => WordBusyGuard.IsBusy;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDarkTitleBar(this, App.UserPrefs.IsDarkTheme);
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _workingList = _temporaryList;
        _headerMetadataService = new DocxHeaderMetadataService(App.HeaderMetadataCachePath, App.HeaderMetadataCacheBackupPath);
        _weeklyScheduleService = new WeeklyScheduleService(App.WeeklyScheduleCachePath, App.WeeklyScheduleCacheBackupPath);
        _appUpdateService = new AppUpdateService();

        _indexService = new ParticipantIndexService(App.Config, _initialsResolver);
        _attendanceImportService = new AttendanceImportService(_searchService);

        ListsListBox.ItemsSource = _savedLists;
        CurrentListParticipantsListBox.ItemsSource = _currentParticipants;
        SearchResultsListBox.ItemsSource = _searchResults;

        _isListPanelOpen = !App.UserPrefs.IsListPanelCollapsed;
        _isDetailPanelOpen = !App.UserPrefs.IsDetailPanelCollapsed;
        _isNotesPanelOpen = !App.UserPrefs.IsNotesPanelCollapsed;
        _lastNotesPanelWidth = App.UserPrefs.NotesPanelWidth;
        _isArchiveSearchEnabled = App.UserPrefs.IsArchiveSearchEnabled;

        LoadLists();
        RestoreWindowState();
        UpdateArchiveAvailability();
        ConfigureRefreshTimer();
        UpdateSearchUi();
        UpdateListPanelState();
        UpdateNotesPanelState();
        UpdateDetailPanelState();
        UpdateWindowWidthConstraints();
        UpdateUiScaleButtons();
        UpdateWorkingListHeader();
        UpdateListsEmptyState();
        Title = $"Acta v{App.DisplayVersion}";
        UpdateStatus("Bereit.");
        _appUpdateService.TryCleanupSuccessfulUpdateArtifactsOnStartup();

        Loaded += async (_, _) => await RefreshIndexAsync(false);
        Loaded += async (_, _) => await BeginStartupUpdateCheckAsync();
        Loaded += (_, _) => UpdateLayoutAlignment();
        SizeChanged += (_, _) => UpdateLayoutAlignment();
        LocationChanged += (_, _) => RefreshSearchResultsPopupPlacement();
        StateChanged += (_, _) => RefreshSearchResultsPopupPlacement();
        MainAreaBorder.SizeChanged += (_, _) => UpdateLayoutAlignment();
        DetailPanelBorder.SizeChanged += (_, _) => UpdateLayoutAlignment();
        ListPanelBorder.SizeChanged += (_, _) => UpdateLayoutAlignment();
        Deactivated += MainWindow_OnDeactivated;
        Closing += MainWindow_OnClosing;
        Closed += MainWindow_OnClosed;
        WordBusyGuard.BusyStateChanged += WordBusyGuard_OnBusyStateChanged;
    }

    public IReadOnlyList<string> VisibleQuickActions => App.Config.VisibleQuickActions;
    public bool ShowStatusTags => App.Config.ShowStatusTags;
    public bool IsDetailPanelOpen => _isDetailPanelOpen;
    public bool IsNotesPanelOpen => _isNotesPanelOpen;
    public bool IsArchiveSearchEnabled => _isArchiveSearchEnabled;
    public bool IsArchiveLoading => _isArchiveLoading;
    public string ArchiveSearchButtonText => _isArchiveLoading ? "Archiv..." : "Archiv";

    private static SavedList CreateWorkingList()
    {
        return new SavedList
        {
            Name = string.Empty,
            Items = new List<SavedListItem>(),
            Modules = ListRepository.NormalizeModules(null)
        };
    }

    private async Task RefreshIndexAsync(bool showSuccessMessage)
    {
        if (_isRefreshing) return;

        try
        {
            SetRefreshBusy(true);
            UpdateStatus("Teilnehmenden-Index wird aktualisiert ...");
            var result = await _indexService.RebuildAsync();
            _mainIndexEntries = result.Entries;
            UpdateArchiveAvailability();
            RebuildCombinedIndex();
            _lastRefreshAt = DateTime.Now;
            RebuildCurrentParticipants();
            UpdateIndexState(result.Warnings);
            RefreshDetailPanel();
            UpdateStatus(showSuccessMessage
                ? $"Index aktualisiert ({_mainIndexEntries.Count} Teilnehmende)."
                : $"Index bereit ({_mainIndexEntries.Count} Teilnehmende).");

            if (_isArchiveSearchEnabled)
            {
                _ = EnsureArchiveLoadedAsync();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("XHub.RefreshIndexAsync", ex);
            UpdateStatus("Index-Aktualisierung fehlgeschlagen.");
            MessageBox.Show($"Der Index konnte nicht aktualisiert werden:\n{ex.Message}", "Acta",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetRefreshBusy(false);
        }
    }

    private void SetRefreshBusy(bool isBusy)
    {
        _isRefreshing = isBusy;
        if (RefreshIndexButton is not null)
        {
            RefreshIndexButton.IsEnabled = !isBusy;
        }

        if (SearchTextBox is not null)
        {
            SearchTextBox.IsReadOnly = isBusy;
        }

        if (EvaluateProgressPanel is not null)
        {
            EvaluateProgressPanel.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        }
    }


    private void TryWriteScheduleDiagnostics()
    {
        try
        {
            _weeklyScheduleService.WriteDiagnostics(
                App.Config.ScheduleRootPath,
                _indexEntries,
                App.WeeklyScheduleDiagnosticsPath,
                App.WeeklyScheduleDiagnosticsBackupPath);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Stundenplan-Diagnose konnte nicht aktualisiert werden: {ex.Message}");
        }
    }
    private void LoadLists()
    {
        _savedLists.Clear();
        foreach (var list in _listRepository.Load())
        {
            _savedLists.Add(list);
        }

        ResetWorkingList(false);
        ListsListBox.SelectedItem = null;
    }

    private void SaveLists()
    {
        _listRepository.Save(_savedLists);
        ListsListBox.Items.Refresh();
        UpdateListsEmptyState();
    }

    private void ResetWorkingList(bool clearDetail)
    {
        _temporaryList = CreateWorkingList();
        ActivateTemporaryList(clearDetail);
    }

    private void ActivateTemporaryList(bool clearDetail)
    {
        _workingList = _temporaryList;
        _loadedSavedListId = null;
        _workingListLabel = "Temporäre Liste";
        RebuildCurrentParticipants();
        UpdateWorkingListHeader();
        if (clearDetail)
        {
            DetailPanel.Clear();
            ClearSelectedParticipant();
        }
    }

    private IReadOnlyList<DetailModuleConfig> GetActiveModules()
    {
        return (_workingList.Modules?.Count > 0 ? _workingList.Modules : ListRepository.NormalizeModules(null))
            .Where(module => module.IsEnabled)
            .OrderBy(module => module.Order)
            .ToList();
    }

    private void UpdateWorkingListHeader()
    {
        var isSaved = _loadedSavedListId is not null;
        var count = _workingList.Items.Count;

        CurrentListTitleTextBlock.Text = string.IsNullOrWhiteSpace(_workingListLabel) ? "Temporäre Liste" : _workingListLabel;
        CurrentListSubtitleTextBlock.Text = count == 0
            ? (isSaved ? "Gespeicherte Liste — leer" : "Keine Teilnehmenden")
            : $"{count} Teilnehmende";

        SavedBadge.Visibility = isSaved ? Visibility.Visible : Visibility.Collapsed;
        SaveWorkingListButton.Content = "Als Liste speichern";
        SaveWorkingListButton.Visibility = !isSaved && count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearWorkingListButton.Visibility = !isSaved && count > 0 ? Visibility.Visible : Visibility.Collapsed;
        WorkingAreaEmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateTemporaryListSummary();

        var isWorkspaceActive = _loadedSavedListId is null;
        WorkspaceButton.BorderBrush = isWorkspaceActive
            ? (Brush)FindResource("Brush.Accent")
            : Brushes.Transparent;
        WorkspaceButton.BorderThickness = new Thickness(1);
        WorkspaceButton.Background = isWorkspaceActive
            ? (Brush)FindResource("Brush.AccentSubtle")
            : Brushes.Transparent;
    }

    private void PersistWorkingListIfSaved()
    {
        if (_loadedSavedListId is null)
        {
            return;
        }

        var savedList = FindListById(_loadedSavedListId);
        if (savedList is null)
        {
            return;
        }

        savedList.Name = _workingListLabel;
        savedList.Items = _workingList.Items
            .Select(item => new SavedListItem
            {
                ParticipantKey = item.ParticipantKey,
                SortOrder = item.SortOrder
            })
            .ToList();
        savedList.Modules = _workingList.Modules.Select(module => module.Clone()).ToList();
        SaveLists();
    }

    private void UpdateSearchUi()
    {
        var hasText = !string.IsNullOrWhiteSpace(SearchTextBox.Text);
        SearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        ClearSearchButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
        SearchResultsPopup.IsOpen = hasText && _searchResults.Count > 0 && !_suppressSearchResultsUntilTyping;
        RefreshSearchResultsPopupPlacement();
    }

    private void RefreshSearchResultsPopupPlacement()
    {
        if (SearchResultsPopup is null || !SearchResultsPopup.IsOpen)
        {
            return;
        }

        var horizontalOffset = SearchResultsPopup.HorizontalOffset;
        SearchResultsPopup.HorizontalOffset = horizontalOffset + 1;
        SearchResultsPopup.HorizontalOffset = horizontalOffset;
    }

    private void UpdateTemporaryListSummary()
    {
        var temporaryCount = _temporaryList.Items.Count;
        WorkspaceCountText.Text = temporaryCount == 0 ? "Leer" : $"{temporaryCount} TN";
    }

    private void UpdateListPanelState()
    {
        if (_isListPanelOpen)
        {
            ListPanelColumn.MinWidth = GetListPanelMinWidth();
            ListPanelColumn.Width = new GridLength(Math.Max(ListPanelColumn.ActualWidth, GetListPanelExpandedWidth()));
            ListPanelSplitterColumn.Width = new GridLength(6);
            ListPanelBorder.Visibility = Visibility.Visible;
            ListPanelSplitter.Visibility = Visibility.Visible;
            MainAreaBorder.Margin = new Thickness(8, 0, 8, 0);
            ToggleListPanelButton.Content = "☰";
        }
        else
        {
            ListPanelColumn.MinWidth = 0;
            ListPanelColumn.Width = new GridLength(0);
            ListPanelSplitterColumn.Width = new GridLength(0);
            ListPanelBorder.Visibility = Visibility.Collapsed;
            ListPanelSplitter.Visibility = Visibility.Collapsed;
            MainAreaBorder.Margin = new Thickness(0, 0, 8, 0);
            ToggleListPanelButton.Content = "☰";
        }

        MainContentColumn.MinWidth = GetMainContentMinWidth();
        UpdateWindowWidthConstraints();
        UpdateLayoutAlignment();
    }

    private void UpdateDetailPanelState()
    {
        if (_isDetailPanelOpen)
        {
            var detailWidth = GetDetailPanelPreferredWidth();
            DetailPanelColumn.MinWidth = detailWidth;
            DetailPanelColumn.Width = new GridLength(detailWidth);
            DetailPanelBorder.Visibility = Visibility.Visible;
            ToggleDetailPanelButton.ToolTip = "Detailbereich ausblenden";
            ToggleDetailPanelButton.Background = (Brush)FindResource("Brush.AccentSubtle");
            ToggleDetailPanelButton.BorderBrush = (Brush)FindResource("Brush.Accent");
            if (_selectedParticipant is null)
            {
                DetailPanel.Clear();
            }
            else
            {
                RefreshDetailPanel();
            }
        }
        else
        {
            DetailPanelColumn.MinWidth = 0;
            DetailPanelColumn.Width = new GridLength(0);
            DetailPanelBorder.Visibility = Visibility.Collapsed;
            ToggleDetailPanelButton.ToolTip = "Detailbereich einblenden";
            ToggleDetailPanelButton.Background = (Brush)FindResource("Brush.CardBg");
            ToggleDetailPanelButton.BorderBrush = (Brush)FindResource("Brush.Border");
        }

        UpdateDetailSplitterState();
        MainContentColumn.MinWidth = GetMainContentMinWidth();
        UpdateWindowWidthConstraints();
        UpdateLayoutAlignment();
    }

    private void UpdateNotesPanelState()
    {
        if (_isNotesPanelOpen)
        {
            var notesWidth = GetRequestedNotesPanelWidth();
            NotesPanelColumn.MinWidth = NotesPanelMinWidth;
            NotesPanelColumn.Width = new GridLength(notesWidth);
            NotesPanelSplitterColumn.Width = new GridLength(6);
            NotesPanelBorder.Visibility = Visibility.Visible;
            NotesPanelSplitter.Visibility = Visibility.Visible;
            ToggleNotesPanelButton.ToolTip = "Notizspalte ausblenden";
            ToggleNotesPanelButton.Background = (Brush)FindResource("Brush.AccentSubtle");
            ToggleNotesPanelButton.BorderBrush = (Brush)FindResource("Brush.Accent");
            NotesPanel.SetParticipant(_selectedParticipant);
        }
        else
        {
            CaptureCurrentNotesPanelWidth();
            NotesPanel.FlushPendingAutosave();
            NotesPanel.SetParticipant(null);
            NotesPanelColumn.MinWidth = 0;
            NotesPanelColumn.Width = new GridLength(0);
            NotesPanelSplitterColumn.Width = new GridLength(0);
            NotesPanelBorder.Visibility = Visibility.Collapsed;
            NotesPanelSplitter.Visibility = Visibility.Collapsed;
            ToggleNotesPanelButton.ToolTip = "Notizspalte einblenden";
            ToggleNotesPanelButton.Background = (Brush)FindResource("Brush.CardBg");
            ToggleNotesPanelButton.BorderBrush = (Brush)FindResource("Brush.Border");
        }

        UpdateDetailSplitterState();
        MainContentColumn.MinWidth = GetMainContentMinWidth();
        UpdateWindowWidthConstraints();
        UpdateLayoutAlignment();
    }

    private double GetRequestedNotesPanelWidth()
    {
        var requestedWidth = _lastNotesPanelWidth ?? App.UserPrefs.NotesPanelWidth ?? NotesPanelDefaultWidth;
        return Math.Max(requestedWidth, NotesPanelMinWidth);
    }

    private void CaptureCurrentNotesPanelWidth()
    {
        if (!_isNotesPanelOpen)
        {
            return;
        }

        var width = NotesPanelColumn.ActualWidth >= NotesPanelMinWidth
            ? NotesPanelColumn.ActualWidth
            : NotesPanelColumn.Width.Value;
        if (width < NotesPanelMinWidth)
        {
            return;
        }

        _lastNotesPanelWidth = width;
        App.UserPrefs.NotesPanelWidth = width;
    }

    private void UpdateDetailSplitterState()
    {
        if (_isDetailPanelOpen && _isNotesPanelOpen)
        {
            DetailPanelSplitterColumn.Width = new GridLength(6);
            DetailPanelSplitter.Visibility = Visibility.Visible;
            return;
        }

        DetailPanelSplitterColumn.Width = new GridLength(0);
        DetailPanelSplitter.Visibility = Visibility.Collapsed;
    }

    private void UpdateWindowWidthConstraints()
    {
        var targetMinWidth = Math.Max(
            GetRequestedWindowMinWidth() + (_isNotesPanelOpen ? NotesPanelWidthContribution : 0),
            GetPanelBasedWindowMinWidth());

        MinWidth = targetMinWidth;
        if (Width < targetMinWidth)
        {
            Width = targetMinWidth;
        }

        UpdateLayoutAlignment();
    }

    private double GetPanelBasedWindowMinWidth()
    {
        const double rootMargin = 20;
        const double layoutBuffer = 28;
        var minimum = rootMargin + layoutBuffer + GetMainContentMinWidth();

        if (_isListPanelOpen)
        {
            minimum += GetListPanelMinWidth() + 6 + 16;
        }

        if (_isNotesPanelOpen)
        {
            minimum += NotesPanelMinWidth + 6;
        }

        if (_isDetailPanelOpen)
        {
            minimum += GetDetailPanelPreferredWidth() + (_isNotesPanelOpen ? 6 : 0);
        }

        return minimum;
    }

    private void UpdateLayoutAlignment()
    {
        UpdateTopBarAlignment();
        UpdateStatusBarAlignment();
    }

    private void UpdateTopBarAlignment()
    {
        if (!IsLoaded || RootGrid.ActualWidth <= 0)
        {
            return;
        }

        TopBarBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        TopBarBorder.Width = double.NaN;
        TopBarBorder.Margin = new Thickness(0, 0, 0, 0);
    }

    private void UpdateStatusBarAlignment()
    {
        if (!IsLoaded || MainAreaBorder.ActualWidth <= 0) return;

        var origin = MainAreaBorder.TransformToAncestor(RootGrid).Transform(new Point(0, 0));
        StatusBarBorder.HorizontalAlignment = HorizontalAlignment.Left;
        StatusBarBorder.Width = MainAreaBorder.ActualWidth;
        StatusBarBorder.Margin = new Thickness(origin.X, 8, 0, 8);
    }

    private void UpdateListsEmptyState()
    {
        ListsEmptyState.Visibility = _savedLists.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RebuildCurrentParticipants()
    {
        _currentParticipants.Clear();
        foreach (var item in _workingList.Items.OrderBy(x => x.SortOrder))
        {
            if (_indexEntriesByParticipantKey.TryGetValue(item.ParticipantKey, out var entry))
            {
                _currentParticipants.Add(entry);
                continue;
            }

            if (ParticipantArchiveService.IsArchivedParticipantPath(item.ParticipantKey, _archiveRootPath))
            {
                _currentParticipants.Add(ParticipantArchiveService.CreateArchiveFallbackEntry(item.ParticipantKey));
                continue;
            }

            _currentParticipants.Add(new ParticipantIndexEntry
            {
                ParticipantKey = item.ParticipantKey,
                DisplayName = Path.GetFileName(item.ParticipantKey),
                FolderPath = item.ParticipantKey,
                StatusTag = string.Empty
            });
        }

        UpdateWorkingListHeader();
        RefreshSearchResults();
    }

    private void RefreshSearchResults()
    {
        _searchResults.Clear();
        var workingKeys = _workingList.Items
            .Select(item => item.ParticipantKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var searchableEntries = _isArchiveSearchEnabled ? _indexEntries : _mainIndexEntries;
        foreach (var result in _searchService.Search(SearchTextBox.Text, searchableEntries))
        {
            if (!workingKeys.Contains(result.ParticipantKey))
            {
                _searchResults.Add(result);
            }
        }

        UpdateSearchUi();
    }

    private void RefreshDetailPanel()
    {
        if (_selectedParticipant is null)
        {
            if (_isDetailPanelOpen)
            {
                DetailPanel.Clear();
            }

            NotesPanel.SetParticipant(null);
            return;
        }

        _selectedParticipant = FindParticipantByKey(_selectedParticipant.ParticipantKey) ?? _selectedParticipant;
        EnrichParticipantDetailMetadata(_selectedParticipant);
        if (_isDetailPanelOpen)
        {
            DetailPanel.UpdateParticipant(_selectedParticipant, GetActiveModules());
        }

        if (_isNotesPanelOpen)
        {
            NotesPanel.SetParticipant(_selectedParticipant);
        }
    }

    private void ShowParticipantDetails(ParticipantIndexEntry entry)
    {
        _selectedParticipant = entry;
        EnrichParticipantDetailMetadata(entry);
        if (_isNotesPanelOpen)
        {
            NotesPanel.SetParticipant(entry);
        }

        if (_isDetailPanelOpen)
        {
            DetailPanel.UpdateParticipant(entry, GetActiveModules());
        }
    }

    private void ClearSelectedParticipant()
    {
        _selectedParticipant = null;
        NotesPanel.SetParticipant(null);
    }

    private void UpdateIndexState(IReadOnlyList<string> warnings)
    {
        var count = _mainIndexEntries.Count + (_archiveIndexEntries.Count > 0 ? _archiveIndexEntries.Count : 0);
        var summary = _lastRefreshAt.HasValue
            ? $"Index: {count} TN, {_lastRefreshAt:HH:mm:ss}"
            : $"Index: {count} TN";
        if (warnings.Count > 0)
        {
            summary += $" | {warnings.Count} Warnungen";
        }

        IndexStateTextBlock.Text = summary;
    }

    private void UpdateArchiveAvailability()
    {
        _archiveRootPath = ParticipantArchiveService.TryGetArchiveRoot(App.Config.ExitBasePath);
        if (!string.Equals(_archiveRootPath, _loadedArchiveRootPath, StringComparison.OrdinalIgnoreCase))
        {
            _archiveIndexEntries = Array.Empty<ParticipantIndexEntry>();
            _loadedArchiveRootPath = null;
        }

        _isArchiveAvailable = !string.IsNullOrWhiteSpace(_archiveRootPath);
        if (!_isArchiveAvailable)
        {
            _isArchiveSearchEnabled = false;
            _isArchiveLoading = false;
            _archiveIndexEntries = Array.Empty<ParticipantIndexEntry>();
            App.UserPrefs.IsArchiveSearchEnabled = false;
        }
        else
        {
            _isArchiveSearchEnabled = App.UserPrefs.IsArchiveSearchEnabled;
        }

        RebuildCombinedIndex();
        UpdateArchiveButtonState();
    }

    private void RebuildCombinedIndex()
    {
        var merged = _mainIndexEntries
            .Concat(_archiveIndexEntries)
            .DistinctBy(entry => entry.ParticipantKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _indexEntries = merged;
        _indexEntriesByParticipantKey = merged.ToDictionary(entry => entry.ParticipantKey, StringComparer.OrdinalIgnoreCase);
    }

    private void UpdateArchiveButtonState()
    {
        if (ArchiveSearchButton is null)
        {
            return;
        }

        ArchiveSearchButton.Visibility = _isArchiveAvailable ? Visibility.Visible : Visibility.Collapsed;
        ArchiveSearchButton.IsEnabled = _isArchiveAvailable && !_isArchiveLoading;
        OnPropertyChanged(nameof(IsArchiveSearchEnabled));
        OnPropertyChanged(nameof(IsArchiveLoading));
        OnPropertyChanged(nameof(ArchiveSearchButtonText));
    }

    private async Task EnsureArchiveLoadedAsync()
    {
        if (!_isArchiveAvailable || string.IsNullOrWhiteSpace(_archiveRootPath) || _isArchiveLoading || _archiveIndexEntries.Count > 0)
        {
            return;
        }

        try
        {
            _isArchiveLoading = true;
            UpdateArchiveButtonState();
            UpdateStatus("Archiv wird geladen...");
            var result = await _archiveService.BuildArchiveAsync(_archiveRootPath);
            _archiveIndexEntries = result.Entries;
            _loadedArchiveRootPath = _archiveRootPath;
            RebuildCombinedIndex();
            RebuildCurrentParticipants();
            RefreshSearchResults();
            RefreshDetailPanel();
            UpdateIndexState(result.Warnings);
            UpdateStatus(result.Warnings.Count > 0
                ? $"Archiv geladen ({_archiveIndexEntries.Count} TN, {result.Warnings.Count} Warnungen)."
                : $"Archiv geladen ({_archiveIndexEntries.Count} TN).");
        }
        catch (Exception ex)
        {
            AppLogger.Error("XHub.Archive.Load", ex);
            UpdateStatus("Archiv konnte nicht geladen werden.");
            MessageBox.Show($"Das Archiv konnte nicht geladen werden:\n{ex.Message}", "Acta",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isArchiveLoading = false;
            UpdateArchiveButtonState();
        }
    }

    private void UpdateStatus(string text) => StatusTextBlock.Text = text;

    private void ConfigureRefreshTimer()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;

        if (App.Config.AutoRefreshHours <= 0) return;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(App.Config.AutoRefreshHours) };
        _refreshTimer.Tick += async (_, _) => await RefreshIndexAsync(false);
        _refreshTimer.Start();
    }

    private void RestoreWindowState()
    {
        var requestedWidth = App.UserPrefs.WindowWidth is > 0 ? App.UserPrefs.WindowWidth.Value : Width;
        var requestedHeight = App.UserPrefs.WindowHeight is > 0 ? App.UserPrefs.WindowHeight.Value : Height;
        ApplyCenteredPrimaryBounds(requestedWidth, requestedHeight);
    }

    private void PersistWindowState()
    {
        var bounds = WindowState == WindowState.Normal ? new Size(Width, Height) : RestoreBounds.Size;

        App.UserPrefs.WindowWidth = bounds.Width;
        App.UserPrefs.WindowHeight = bounds.Height;
        App.UserPrefs.UiScaleLevel = CurrentUiScaleLevel;
        App.UserPrefs.IsListPanelCollapsed = !_isListPanelOpen;
        App.UserPrefs.IsDetailPanelCollapsed = !_isDetailPanelOpen;
        App.UserPrefs.IsNotesPanelCollapsed = !_isNotesPanelOpen;
        App.UserPrefs.IsArchiveSearchEnabled = _isArchiveSearchEnabled && _isArchiveAvailable;
        CaptureCurrentNotesPanelWidth();
        App.SaveUserPrefs();
    }

    private void SearchTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _suppressSearchResultsUntilTyping = false;
        RefreshSearchResults();
    }

    private void ClearSearchButton_OnClick(object sender, RoutedEventArgs e)
    {
        _suppressSearchResultsUntilTyping = false;
        SearchTextBox.Clear();
        SearchTextBox.Focus();
    }

    private async void ArchiveSearchButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_isArchiveAvailable)
        {
            return;
        }

        _isArchiveSearchEnabled = !_isArchiveSearchEnabled;
        App.UserPrefs.IsArchiveSearchEnabled = _isArchiveSearchEnabled;
        App.SaveUserPrefs();
        UpdateArchiveButtonState();
        RefreshSearchResults();

        if (_isArchiveSearchEnabled)
        {
            await EnsureArchiveLoadedAsync();
        }
    }

    private void SearchTextBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchTextBox.Text)) return;
        _suppressSearchResultsUntilTyping = false;
        RefreshSearchResults();
    }

    private void RootGrid_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (IsDescendantOf(source, SearchTextBox)) return;
        if (IsDescendantOf(source, ClearSearchButton)) return;
        if (IsDescendantOf(source, ArchiveSearchButton)) return;
        if (string.IsNullOrWhiteSpace(SearchTextBox.Text)) return;

        _suppressSearchResultsUntilTyping = true;
        SearchResultsListBox.SelectedItem = null;
        UpdateSearchUi();
    }
    private void SearchResultsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is not ParticipantIndexEntry entry)
        {
            return;
        }

        AddParticipantToWorkingList(entry, clearSearchText: true, focusSearchBox: true, selectInWorkingList: false, openDetails: false);
        SearchResultsListBox.SelectedItem = null;
    }

    private void CurrentListParticipantsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentListParticipantsListBox.SelectedItem is ParticipantIndexEntry entry)
        {
            ShowParticipantDetails(entry);
        }
    }

    private void CurrentListParticipantsListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CurrentListParticipantsListBox.SelectedItem is not ParticipantIndexEntry entry)
        {
            return;
        }

        var isSameParticipant = _selectedParticipant is not null &&
                                string.Equals(_selectedParticipant.ParticipantKey, entry.ParticipantKey, StringComparison.OrdinalIgnoreCase);

        if (_isDetailPanelOpen && isSameParticipant)
        {
            _isDetailPanelOpen = false;
            UpdateDetailPanelState();
            return;
        }

        if (!_isDetailPanelOpen)
        {
            _isDetailPanelOpen = true;
            UpdateDetailPanelState();
        }

        ShowParticipantDetails(entry);
    }
    private void QuickActionButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { Tag: string actionKey, DataContext: ParticipantIndexEntry participant }) return;

        ExecuteQuickAction(participant, actionKey);
    }

    private void RemoveParticipantButton_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { Tag: string participantKey }) return;

        RemoveParticipantByKey(participantKey);
    }

    private void RemoveParticipantButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string participantKey }) return;
        RemoveParticipantByKey(participantKey);
    }

    private void ClearWorkingListButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetWorkingList(true);
        ListsListBox.SelectedItem = null;
        UpdateStatus("Temporäre Liste geleert.");
    }

    private void SaveWorkingListButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_workingList.Items.Count == 0) return;

        var initialName = _workingListLabel == "Temporäre Liste" ? string.Empty : _workingListLabel;
        var dialog = new TextPromptWindow("Liste speichern", "Listenname:", initialName) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;

        var targetName = dialog.Value.Trim();
        var existing = _loadedSavedListId is null ? null : FindListById(_loadedSavedListId);
        if (existing is null)
        {
            existing = _savedLists.FirstOrDefault(list =>
                string.Equals(list.Name, targetName, StringComparison.CurrentCultureIgnoreCase));
        }
        else if (!string.Equals(existing.Name, targetName, StringComparison.CurrentCultureIgnoreCase))
        {
            var byName = _savedLists.FirstOrDefault(list =>
                string.Equals(list.Name, targetName, StringComparison.CurrentCultureIgnoreCase) &&
                !string.Equals(list.Id, existing.Id, StringComparison.OrdinalIgnoreCase));
            existing = byName ?? existing;
        }

        if (existing is not null &&
            !string.Equals(existing.Id, _loadedSavedListId, StringComparison.OrdinalIgnoreCase))
        {
            if (MessageBox.Show($"Liste '{targetName}' überschreiben?", "Acta",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
        }

        var wasTemporaryList = _loadedSavedListId is null;
        var target = existing ?? new SavedList { SortOrder = _savedLists.Count };
        target.Name = targetName;
        target.Items = _workingList.Items
            .Select(item => new SavedListItem { ParticipantKey = item.ParticipantKey, SortOrder = item.SortOrder })
            .ToList();
        target.Modules = _workingList.Modules.Select(module => module.Clone()).ToList();

        if (existing is null) _savedLists.Add(target);

        for (var i = 0; i < _savedLists.Count; i++) _savedLists[i].SortOrder = i;

        SaveLists();

        if (wasTemporaryList)
        {
            _temporaryList = CloneList(_workingList);
        }

        ActivateSavedList(target);
        ListsListBox.SelectedItem = _savedLists.FirstOrDefault(l =>
            string.Equals(l.Id, target.Id, StringComparison.OrdinalIgnoreCase));

        UpdateStatus($"Liste gespeichert: {target.Name}");
    }
    private void ToggleListPanelButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isListPanelOpen = !_isListPanelOpen;
        UpdateListPanelState();
    }

    private void ToggleDetailPanelButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isDetailPanelOpen = !_isDetailPanelOpen;
        UpdateDetailPanelState();
    }

    private void ToggleNotesPanelButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isNotesPanelOpen)
        {
            CaptureCurrentNotesPanelWidth();
        }

        _isNotesPanelOpen = !_isNotesPanelOpen;
        UpdateNotesPanelState();
    }

    private void DecreaseUiScaleButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyUiScaleLevel(CurrentUiScaleLevel - 1);
    }

    private void IncreaseUiScaleButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyUiScaleLevel(CurrentUiScaleLevel + 1);
    }

    private void CloseDetailPanelButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_isDetailPanelOpen) return;
        _isDetailPanelOpen = false;
        UpdateDetailPanelState();
    }

    private void WorkspaceButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_loadedSavedListId is null) return;

        ActivateTemporaryList(true);
        ListsListBox.SelectedItem = null;
        UpdateStatus("Temporäre Liste aktiv.");
    }

    private void NewListButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TextPromptWindow("Neue Liste", "Listenname:", string.Empty) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;

        var newList = new SavedList
        {
            Name = dialog.Value.Trim(),
            SortOrder = _savedLists.Count
        };
        _savedLists.Add(newList);
        SaveLists();

        ActivateSavedList(newList);
        ListsListBox.SelectedItem = newList;
        UpdateStatus($"Neue Liste erstellt: {newList.Name}");
    }

    private void ListsListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListsListBox.SelectedItem is not SavedList selected) return;
        if (string.Equals(_loadedSavedListId, selected.Id, StringComparison.OrdinalIgnoreCase)) return;

        ActivateSavedList(selected);
    }

    private void ActivateSavedList(SavedList list)
    {
        _loadedSavedListId = list.Id;
        _workingListLabel = list.Name;
        _workingList = CloneList(list);
        RebuildCurrentParticipants();
        DetailPanel.Clear();
        ClearSelectedParticipant();
        UpdateStatus($"Liste geladen: {list.Name}");
    }

    private void ListOptionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is { } menu)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void RenameListMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string listId }) return;
        var list = FindListById(listId);
        if (list is null) return;

        var dialog = new TextPromptWindow("Liste umbenennen", "Neuer Listenname:", list.Name) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Value)) return;

        list.Name = dialog.Value.Trim();
        SaveLists();
        if (string.Equals(_loadedSavedListId, list.Id, StringComparison.OrdinalIgnoreCase))
        {
            _workingListLabel = list.Name;
            UpdateWorkingListHeader();
        }

        UpdateStatus($"Liste umbenannt: {list.Name}");
    }

    private void DeleteListMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string listId }) return;
        var list = FindListById(listId);
        if (list is null) return;

        if (MessageBox.Show($"Liste '{list.Name}' wirklich löschen?", "Acta",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _savedLists.Remove(list);
        for (var i = 0; i < _savedLists.Count; i++) _savedLists[i].SortOrder = i;
        SaveLists();

        if (string.Equals(_loadedSavedListId, list.Id, StringComparison.OrdinalIgnoreCase))
        {
            ActivateTemporaryList(true);
        }

        UpdateStatus("Liste gelöscht.");
    }

    private void ConfigureModulesMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string listId }) return;
        var list = FindListById(listId);
        if (list is null) return;

        var dialog = new ModuleSettingsWindow(list.Modules) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        list.Modules = dialog.Result;
        SaveLists();
        if (string.Equals(_loadedSavedListId, list.Id, StringComparison.OrdinalIgnoreCase))
        {
            _workingList.Modules = dialog.Result.Select(module => module.Clone()).ToList();
            RefreshDetailPanel();
        }

        UpdateStatus($"Module für '{list.Name}' aktualisiert.");
    }

    private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ = RefreshIndexAsync(true);
    }

    private async void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(App.Config, App.UserPrefs) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        switch (dialog.Result.RequestedAction)
        {
            case SettingsWindowAction.Export:
                ExecuteExport();
                return;
            case SettingsWindowAction.Import:
                ExecuteImport();
                return;
        }

        App.Config.ServerBasePath = dialog.Result.LvPath;
        App.Config.LvBasePath = dialog.Result.LvPath;
        App.Config.LbBasePath = dialog.Result.LbPath;
        App.Config.StartBasePath = dialog.Result.StartPath;
        App.Config.ExitBasePath = dialog.Result.ExitPath;
        App.Config.ScheduleRootPath = dialog.Result.SchedulePath;
        App.Config.AutoRefreshHours = dialog.Result.AutoRefreshHours;
        App.Config.ShowStatusTags = dialog.Result.ShowStatusTags;
        App.Config.ShowParticipantPhoto = dialog.Result.ShowParticipantPhoto;
        App.Config.VisibleQuickActions = dialog.Result.VisibleQuickActions.ToList();
        App.SaveConfig();

        App.UserPrefs.ShowMiniSchedule = dialog.Result.ShowMiniSchedule;
        _isNotesPanelOpen = dialog.Result.ShowNotesPanel;
        App.UserPrefs.IsNotesPanelCollapsed = !_isNotesPanelOpen;
        App.ApplyTheme(dialog.Result.IsDarkTheme);
        UpdateNotesPanelState();
        UpdateDetailPanelState();
        App.SaveUserPrefs();
        DataContext = null;
        DataContext = this;

        _indexService = new ParticipantIndexService(App.Config, _initialsResolver);
        _attendanceImportService = new AttendanceImportService(_searchService);
        ConfigureRefreshTimer();
        RefreshDetailPanel();
        RefreshSearchResults();
        await RefreshIndexAsync(true);
    }

    private void ImportAttendanceButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new AttendanceImportWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var result = _attendanceImportService.Import(dialog.RawText, _indexEntries);

            _temporaryList = CloneList(result.ImportedList);
            _workingList = _temporaryList;
            _loadedSavedListId = null;
            _workingListLabel = "Temporäre Liste";
            RebuildCurrentParticipants();
            DetailPanel.Clear();
            ClearSelectedParticipant();
            ListsListBox.SelectedItem = null;
            UpdateStatus($"Import: {result.MatchedCount}/{result.ParsedLineCount} gematcht.");
            if (result.UnmatchedLines.Count > 0)
            {
                MessageBox.Show(
                    $"Nicht zugeordnet ({result.UnmatchedLines.Count}):\n\n{string.Join("\n", result.UnmatchedLines.Take(12))}",
                    "Acta", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("XHub.ImportAttendance", ex);
            MessageBox.Show($"Import fehlgeschlagen:\n{ex.Message}", "Acta",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void ExecuteExport()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Acta Export (*.xhub.json)|*.xhub.json|JSON (*.json)|*.json",
            FileName = $"xhub-lists-{DateTime.Now:yyyyMMdd-HHmm}.xhub.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            _exportService.Export(dialog.FileName, _savedLists);
            UpdateStatus("Listen exportiert.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("XHub.Export", ex);
            MessageBox.Show($"Export fehlgeschlagen:\n{ex.Message}", "Acta",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteImport()
    {
        var dialog = new OpenFileDialog
            { Filter = "Acta Export (*.xhub.json;*.json)|*.xhub.json;*.json" };
        if (dialog.ShowDialog(this) != true) return;
        if (MessageBox.Show("Der Import ersetzt die aktuellen lokalen Listen. Fortfahren?", "Acta",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            var imported = _exportService.Import(dialog.FileName);
            _savedLists.Clear();
            foreach (var list in imported.OrderBy(list => list.SortOrder))
            {
                _savedLists.Add(list);
            }

            SaveLists();
            ListsListBox.SelectedItem = null;
            ActivateTemporaryList(true);
            UpdateStatus("Listen importiert.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("XHub.Import", ex);
            MessageBox.Show($"Import fehlgeschlagen:\n{ex.Message}", "Acta",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool AddParticipantToWorkingList(
        ParticipantIndexEntry entry,
        bool clearSearchText,
        bool focusSearchBox,
        bool selectInWorkingList,
        bool openDetails)
    {
        if (_workingList.Items.Any(item =>
                string.Equals(item.ParticipantKey, entry.ParticipantKey, StringComparison.OrdinalIgnoreCase)))
        {
            UpdateStatus("Teilnehmender ist bereits vorhanden.");
            return false;
        }

        _workingList.Items.Add(new SavedListItem
        {
            ParticipantKey = entry.ParticipantKey,
            SortOrder = _workingList.Items.Count
        });
        RebuildCurrentParticipants();
        PersistWorkingListIfSaved();

        var added = _currentParticipants.FirstOrDefault(candidate =>
            string.Equals(candidate.ParticipantKey, entry.ParticipantKey, StringComparison.OrdinalIgnoreCase));

        if (selectInWorkingList && added is not null)
        {
            CurrentListParticipantsListBox.SelectedItem = added;
        }

        if (openDetails && added is not null && _isDetailPanelOpen)
        {
            ShowParticipantDetails(added);
        }

        if (clearSearchText)
        {
            SearchTextBox.Clear();
        }

        if (focusSearchBox)
        {
            SearchTextBox.Focus();
            SearchTextBox.CaretIndex = SearchTextBox.Text.Length;
        }

        UpdateStatus("Teilnehmender hinzugefügt.");
        return true;
    }

    private void RemoveParticipantByKey(string participantKey)
    {
        var item = _workingList.Items.FirstOrDefault(listItem =>
            string.Equals(listItem.ParticipantKey, participantKey, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;

        _workingList.Items.Remove(item);
        ResequenceItems(_workingList);
        RebuildCurrentParticipants();
        PersistWorkingListIfSaved();

        if (_selectedParticipant is not null &&
            string.Equals(_selectedParticipant.ParticipantKey, participantKey, StringComparison.OrdinalIgnoreCase))
        {
            DetailPanel.Clear();
            ClearSelectedParticipant();
        }

        UpdateStatus("Teilnehmender entfernt.");
    }

    private void ExecuteQuickAction(ParticipantIndexEntry entry, string actionKey)
    {
        switch (actionKey)
        {
            case QuickActionKeys.Folder:
                OpenFolder(entry);
                break;
            case QuickActionKeys.Document:
                OpenAkte(entry);
                break;
            case QuickActionKeys.Bu:
                OpenBookmark(entry, App.Config.WordBuBookmarkName);
                break;
            case QuickActionKeys.Bi:
                OpenBookmark(entry, App.Config.WordBiBookmarkName);
                break;
            case QuickActionKeys.Be:
                OpenBookmark(entry, App.Config.WordBeBookmarkName);
                break;
            case QuickActionKeys.Lb:
                OpenBookmark(entry, App.Config.WordLbBookmarkName);
                break;
        }
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
            AppLogger.Error($"XHub.MainWindow.OpenFolder '{entry.FolderPath}'", ex);
            MessageBox.Show("Der Teilnehmerordner konnte nicht geöffnet werden.", "Acta", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenAkte(ParticipantIndexEntry entry) =>
        TryWordAction(entry, OpenDocumentViaShellWithCooldownAsync);

    private void OpenBookmark(ParticipantIndexEntry entry, string bookmark) =>
        TryWordAction(entry, path => App.WordStaHost.RunAsync(
            $"OpenDocumentAtBookmark:{bookmark}",
            service => service.OpenDocumentAtBookmark(path, bookmark)));

    private async void TryWordAction(ParticipantIndexEntry entry, Func<string, Task> action)
    {
        if (!WordBusyGuard.TryEnter())
        {
            UpdateStatus("Eine Word-Aktion läuft bereits...");
            return;
        }

        const string openingStatus = "Öffne Dokument...";
        var previousStatus = StatusTextBlock.Text;
        var previousCursor = Mouse.OverrideCursor;

        try
        {
            var documentPath = ResolveDocumentPath(entry);
            UpdateStatus(openingStatus);
            Mouse.OverrideCursor = Cursors.AppStarting;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            await action(documentPath);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"XHub.MainWindow.WordAction '{entry.DisplayName}'", ex);
            MessageBox.Show(ex.Message, "Acta", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = previousCursor;
            if (StatusTextBlock.Text == openingStatus)
            {
                UpdateStatus(previousStatus);
            }

            WordBusyGuard.Exit();
        }
    }

    private static async Task OpenDocumentViaShellWithCooldownAsync(string documentPath)
    {
        WordService.OpenDocumentViaShell(documentPath);
        await Task.Delay(WordService.NativeOpenCooldownMs);
    }

    private void EnrichParticipantDetailMetadata(ParticipantIndexEntry entry)
    {
        EnrichParticipantHeaderMetadata(entry);
        EnrichParticipantSchedule(entry);
    }

    private void EnrichParticipantSchedule(ParticipantIndexEntry entry)
    {
        if (entry.IsArchived || !App.UserPrefs.ShowMiniSchedule)
        {
            entry.MiniSchedule = new ParticipantMiniScheduleSummary
            {
                State = ParticipantMiniScheduleState.Hidden
            };
            return;
        }

        try
        {
            var scheduleParticipants = _indexEntries
                .Where(candidate => !candidate.IsArchived)
                .ToList();
            entry.MiniSchedule = _weeklyScheduleService.GetParticipantSchedule(
                App.Config.ScheduleRootPath,
                entry,
                scheduleParticipants);

            if (entry.MiniSchedule.State == ParticipantMiniScheduleState.Unavailable)
            {
                AppLogger.Info(
                    $"Stundenplan: Kein Mini-Stundenplan fuer '{entry.DisplayName}'. Grund='{entry.MiniSchedule.Message}'.");
            }
        }
        catch (Exception ex)
        {
            entry.MiniSchedule = new ParticipantMiniScheduleSummary
            {
                State = ParticipantMiniScheduleState.Unavailable,
                Message = "Kein Stundenplan"
            };
            AppLogger.Warn($"Stundenplan konnte nicht geladen werden '{entry.DisplayName}': {ex.Message}");
        }
    }
    private void EnrichParticipantHeaderMetadata(ParticipantIndexEntry entry)
    {
        if (entry.IsArchived)
        {
            entry.OdooUrl = string.Empty;
            entry.CounselorInitials = string.Empty;
            return;
        }

        try
        {
            var docPath = ResolveDocumentPath(entry, refreshDetailPanel: false);
            var metadata = _headerMetadataService.Read(docPath);
            entry.OdooUrl = metadata.OdooUrl;
            entry.CounselorInitials = metadata.CounselorInitials;
        }
        catch (Exception ex)
        {
            entry.OdooUrl = string.Empty;
            entry.CounselorInitials = string.Empty;
            AppLogger.Warn($"Header-Metadaten konnten nicht geladen werden '{entry.DisplayName}': {ex.Message}");
        }
    }

    private string ResolveDocumentPath(ParticipantIndexEntry entry, bool refreshDetailPanel = true)
    {
        if (!string.IsNullOrWhiteSpace(entry.DocumentPath) && File.Exists(entry.DocumentPath))
            return entry.DocumentPath;
        if (!Directory.Exists(entry.FolderPath))
            throw new InvalidOperationException("Teilnehmerordner ist nicht erreichbar.");

        var docPath = WordService.FindVerlaufsakte(entry.FolderPath, App.Config.VerlaufsakteKeyword);
        entry.DocumentPath = docPath;
        entry.Initials = _initialsResolver.TryResolveFromDocumentPath(docPath);
        if (refreshDetailPanel)
        {
            RefreshDetailPanel();
        }
        return docPath;
    }

    private ParticipantIndexEntry? FindParticipantByKey(string participantKey)
    {
        return _indexEntries.FirstOrDefault(entry =>
                   string.Equals(entry.ParticipantKey, participantKey, StringComparison.OrdinalIgnoreCase))
               ?? _currentParticipants.FirstOrDefault(entry =>
                   string.Equals(entry.ParticipantKey, participantKey, StringComparison.OrdinalIgnoreCase));
    }

    private SavedList? FindListById(string listId)
    {
        return _savedLists.FirstOrDefault(list =>
            string.Equals(list.Id, listId, StringComparison.OrdinalIgnoreCase));
    }

    private static SavedList CloneList(SavedList source)
    {
        return new SavedList
        {
            Name = source.Name,
            Items = source.Items
                .Select(item => new SavedListItem { ParticipantKey = item.ParticipantKey, SortOrder = item.SortOrder })
                .ToList(),
            Modules = source.Modules.Select(module => module.Clone()).ToList()
        };
    }

    private static void ResequenceItems(SavedList list)
    {
        for (var i = 0; i < list.Items.Count; i++) list.Items[i].SortOrder = i;
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor)) return true;

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                Visual3D visual3D => VisualTreeHelper.GetParent(visual3D),
                FrameworkContentElement frameworkContent => frameworkContent.Parent,
                ContentElement contentElement => ContentOperations.GetParent(contentElement) ?? LogicalTreeHelper.GetParent(contentElement),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return false;
    }

    private void ApplyCenteredPrimaryBounds(double requestedWidth, double requestedHeight)
    {
        var workingArea = SystemParameters.WorkArea;
        var minimumWidth = GetRequestedWindowMinWidth();
        var clampedWidth = Math.Min(Math.Max(requestedWidth, minimumWidth), workingArea.Width);
        var clampedHeight = Math.Min(Math.Max(requestedHeight, MinHeight), workingArea.Height);
        var freeVerticalSpace = Math.Max(0, workingArea.Height - clampedHeight);

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = clampedWidth;
        Height = clampedHeight;
        Left = workingArea.Left + Math.Max(0, (workingArea.Width - clampedWidth) / 2);
        Top = workingArea.Top + (freeVerticalSpace * 0.20);
    }

    private double GetRequestedWindowMinWidth() => _isListPanelOpen && _isDetailPanelOpen
        ? GetFullPanelsWindowMinWidth()
        : _isDetailPanelOpen
            ? GetDetailPanelWindowMinWidth()
            : _isListPanelOpen
                ? GetListPanelWindowMinWidth()
                : GetCompactWindowMinWidth();

    private double GetCompactWindowMinWidth() => CurrentUiScaleLevel switch
    {
        1 => 340,
        2 => 360,
        3 => 380,
        4 => BaseCompactWindowMinWidth,
        _ => 460
    };

    private double GetListPanelWindowMinWidth() => CurrentUiScaleLevel switch
    {
        1 => 440,
        2 => 460,
        3 => 490,
        4 => BaseListPanelWindowMinWidth,
        _ => 600
    };

    private double GetDetailPanelWindowMinWidth() => CurrentUiScaleLevel switch
    {
        1 => 580,
        2 => 610,
        3 => 650,
        4 => BaseDetailPanelWindowMinWidth,
        _ => 780
    };

    private double GetFullPanelsWindowMinWidth() => CurrentUiScaleLevel switch
    {
        1 => 740,
        2 => 780,
        3 => 830,
        4 => BaseFullPanelsWindowMinWidth,
        _ => 980
    };

    private double GetListPanelExpandedWidth() => CurrentUiScaleLevel switch
    {
        1 => 172,
        2 => 180,
        3 => 188,
        4 => 200,
        _ => 216
    };

    private double GetListPanelMinWidth() => CurrentUiScaleLevel switch
    {
        1 => 112,
        2 => 120,
        3 => 124,
        4 => 132,
        _ => 146
    };

    private double GetMainContentMinWidth() => CurrentUiScaleLevel switch
    {
        1 => 300,
        2 => 320,
        3 => 340,
        4 => 360,
        _ => 390
    };

    private double GetDetailPanelPreferredWidth() => CurrentUiScaleLevel switch
    {
        1 => 238,
        2 => 250,
        3 => 264,
        4 => 278,
        _ => 300
    };

    private void ApplyUiScaleLevel(int level)
    {
        var normalizedLevel = App.NormalizeUiScaleLevel(level);
        App.ApplyUiScale(normalizedLevel);
        App.SaveUserPrefs();
        UpdateUiScaleButtons();
        UpdateListPanelState();
        UpdateNotesPanelState();
        UpdateDetailPanelState();
        UpdateWindowWidthConstraints();
        UpdateWorkingListHeader();
        RefreshDetailPanel();
        UpdateLayoutAlignment();
    }

    private void UpdateUiScaleButtons()
    {
        var level = CurrentUiScaleLevel;
        if (DecreaseUiScaleButton is not null)
        {
            DecreaseUiScaleButton.IsEnabled = level > 1;
        }

        if (IncreaseUiScaleButton is not null)
        {
            IncreaseUiScaleButton.IsEnabled = level < 5;
        }
    }

    private void MainWindow_OnDeactivated(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SearchTextBox.Text)) return;
        _suppressSearchResultsUntilTyping = true;
        UpdateSearchUi();
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        NotesPanel.FlushPendingAutosave();
        PersistWindowState();
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        WordBusyGuard.BusyStateChanged -= WordBusyGuard_OnBusyStateChanged;
    }

    private void WordBusyGuard_OnBusyStateChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(WordBusyGuard_OnBusyStateChanged, sender, e);
            return;
        }

        OnPropertyChanged(nameof(IsWordActionRunning));
    }

    private async Task BeginStartupUpdateCheckAsync()
    {
        var availableRelease = await _appUpdateService.GetAvailableUpdateAsync(CancellationToken.None);
        if (availableRelease is null || !IsLoaded || _isUpdateShutdownRequested)
        {
            return;
        }

        await Dispatcher.InvokeAsync(() =>
        {
            var updateDialog = new AppUpdateWindow(_appUpdateService, availableRelease)
            {
                Owner = this
            };

            var dialogResult = updateDialog.ShowDialog();
            if (dialogResult == true && updateDialog.DownloadedUpdate is not null)
            {
                TryStartDownloadedUpdate(updateDialog.DownloadedUpdate);
                return;
            }

            if (updateDialog.WasDeferred)
            {
                _appUpdateService.SnoozeRelease(availableRelease);
            }
        });
    }

    private bool TryStartDownloadedUpdate(DownloadedUpdateInfo downloadedUpdate)
    {
        try
        {
            _appUpdateService.LaunchUpdater(downloadedUpdate);
            AppLogger.Info("Updater: Update-Shutdown gestartet.");
            _isUpdateShutdownRequested = true;
            (Application.Current as App)?.PrepareForUpdateShutdown();
            Close();
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Updater: Updater konnte nicht gestartet werden.", ex);
            MessageBox.Show(
                $"Das Update wurde bereits heruntergeladen, konnte aber nicht übernommen werden:\n{ex.Message}",
                "Update konnte nicht gestartet werden",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


























































