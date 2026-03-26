using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using XHub.Models;
using XHub.Services;

namespace XHub;

public partial class App : Application
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hwnd);

    private const int MinUiScaleLevel = 1;
    private const int MaxUiScaleLevel = 5;
    private const int DefaultUiScaleLevel = 2;
    private const int CurrentUiScaleSchemaVersion = 2;
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private static readonly string SingleInstanceMutexName = $@"Local\Acta.SingleInstance.{Environment.UserName}";
    private static readonly string SingleInstancePipeName = $"Acta.SingleInstance.{Environment.UserName}";
    private Mutex? _singleInstanceMutex;
    private CancellationTokenSource? _singleInstanceListenerCancellation;
    private Task? _singleInstanceListenerTask;
    private bool _ownsSingleInstanceMutex;

    public static int NormalizeUiScaleLevel(int level) => Math.Clamp(level, MinUiScaleLevel, MaxUiScaleLevel);

    /// <summary>
    /// Setzt die dunkle Titelleiste per Windows DWM API (Windows 10 20H1+).
    /// </summary>
    public static void ApplyDarkTitleBar(Window window, bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int value = isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, 20, ref value, Marshal.SizeOf(value));
            DwmSetWindowAttribute(hwnd, 19, ref value, Marshal.SizeOf(value));
        }
        catch { }
    }

    private const string DefaultServerBasePath = @"K:\FuturX\20_TNinnen";
    private const string DefaultLbBasePath = @"K:\FuturX\20_TNinnen\02_Lehrbegleitung";
    private const string DefaultScheduleRootPath = @"K:\FuturX\10_Arbeitsplanung\20_Planung\22_Wochenplanung\Einteilung TN";
    public static string AppDataDirectoryPath { get; private set; } = string.Empty;
    public static string SettingsPath { get; private set; } = string.Empty;
    public static string SettingsBackupPath { get; private set; } = string.Empty;
    public static string UserPrefsPath { get; private set; } = string.Empty;
    public static string UserPrefsBackupPath { get; private set; } = string.Empty;
    public static string ListsPath { get; private set; } = string.Empty;
    public static string ListsBackupPath { get; private set; } = string.Empty;
    public static string HeaderMetadataCachePath { get; private set; } = string.Empty;
    public static string HeaderMetadataCacheBackupPath { get; private set; } = string.Empty;
    public static string WeeklyScheduleCachePath { get; private set; } = string.Empty;
    public static string WeeklyScheduleCacheBackupPath { get; private set; } = string.Empty;
    public static string WeeklyScheduleDiagnosticsPath { get; private set; } = string.Empty;
    public static string WeeklyScheduleDiagnosticsBackupPath { get; private set; } = string.Empty;
    public static string DisplayVersion { get; } = ResolveDisplayVersion();
    public static AppConfig Config { get; private set; } = new();
    public static UserPrefs UserPrefs { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!TryAcquireSingleInstance())
        {
            SignalRunningInstance();
            Shutdown(0);
            return;
        }

        AppDataDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XHub");
        Directory.CreateDirectory(AppDataDirectoryPath);

        SettingsPath = Path.Combine(AppDataDirectoryPath, "settings.json");
        SettingsBackupPath = Path.Combine(AppDataDirectoryPath, "settings.bak");
        UserPrefsPath = Path.Combine(AppDataDirectoryPath, "user-prefs.json");
        UserPrefsBackupPath = Path.Combine(AppDataDirectoryPath, "user-prefs.bak");
        ListsPath = Path.Combine(AppDataDirectoryPath, "lists.json");
        ListsBackupPath = Path.Combine(AppDataDirectoryPath, "lists.bak");
        HeaderMetadataCachePath = Path.Combine(AppDataDirectoryPath, "header-metadata-cache.json");
        HeaderMetadataCacheBackupPath = Path.Combine(AppDataDirectoryPath, "header-metadata-cache.bak");
        WeeklyScheduleCachePath = Path.Combine(AppDataDirectoryPath, "weekly-schedule-cache.json");
        WeeklyScheduleCacheBackupPath = Path.Combine(AppDataDirectoryPath, "weekly-schedule-cache.bak");
        var diagnosticsDirectory = Path.Combine(AppDataDirectoryPath, "diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);
        WeeklyScheduleDiagnosticsPath = Path.Combine(diagnosticsDirectory, "weekly-schedule-diagnostics.json");
        WeeklyScheduleDiagnosticsBackupPath = Path.Combine(diagnosticsDirectory, "weekly-schedule-diagnostics.bak");

        RegisterGlobalExceptionLogging();

        var configService = new AppConfigService(SettingsPath, SettingsBackupPath, CreateDefaultConfig());
        Config = configService.Load();
        NormalizeConfig(Config);
        TryPersistNormalizedConfig(configService);

        var hasExistingUserPrefs = File.Exists(UserPrefsPath) || File.Exists(UserPrefsBackupPath);
        var prefsService = new UserPrefsService(UserPrefsPath, UserPrefsBackupPath);
        UserPrefs = prefsService.Load();
        NormalizeUserPrefs(UserPrefs, hasExistingUserPrefs);
        ApplyTheme(UserPrefs.IsDarkTheme);
        ApplyUiScale(UserPrefs.UiScaleLevel);
        TryPersistNormalizedUserPrefs(prefsService);

        base.OnStartup(e);

        var window = new MainWindow();
        window.Show();
        StartSingleInstanceListener();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceListenerCancellation?.Cancel();
            _singleInstanceListenerTask?.Wait(TimeSpan.FromMilliseconds(250));
        }
        catch
        {
            // Best effort cleanup only.
        }
        finally
        {
            _singleInstanceListenerCancellation?.Dispose();
            if (_ownsSingleInstanceMutex && _singleInstanceMutex is not null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
            _singleInstanceMutex?.Dispose();
        }

        base.OnExit(e);
    }

    public static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            ServerBasePath = DefaultServerBasePath,
            UseSecondaryServerBasePath = false,
            SecondaryServerBasePath = string.Empty,
            LvBasePath = DefaultServerBasePath,
            LbBasePath = DefaultLbBasePath,
            StartBasePath = string.Empty,
            ExitBasePath = string.Empty,
            VerlaufsakteKeyword = "Verlaufsakte",
            WordBuBookmarkName = "_Bildung",
            WordBiBookmarkName = "_Berufsintegration",
            WordBeBookmarkName = "_Beratung",
            WordLbBookmarkName = "_Lehrbetrieb",
            WordEntryBuBookmarkName = "_Eintrag_Bildung",
            WordEntryBiBookmarkName = "_Eintrag_Berufsintegration",
            VisibleQuickActions = QuickActionKeys.CreateDefaults().ToList(),
            AutoRefreshHours = 0,
            ShowStatusTags = true,
            ScheduleRootPath = DefaultScheduleRootPath
        };
    }

    public static void SaveConfig()
    {
        try
        {
            new AppConfigService(SettingsPath, SettingsBackupPath, CreateDefaultConfig()).Save(Config);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Konfiguration konnte nicht gespeichert werden: {ex.Message}");
        }
    }

    public static void SaveUserPrefs()
    {
        try
        {
            new UserPrefsService(UserPrefsPath, UserPrefsBackupPath).Save(UserPrefs);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Benutzereinstellungen konnten nicht gespeichert werden: {ex.Message}");
        }
    }

    public static void ApplyTheme(bool isDark)
    {
        UserPrefs.IsDarkTheme = isDark;
        var resources = Current.Resources;

        SetBrush(resources, "Brush.WindowBg", isDark ? "#485161" : "#F4F6F8");
        SetBrush(resources, "Brush.PanelBg", isDark ? "#556072" : "#FFFFFF");
        SetBrush(resources, "Brush.CardBg", isDark ? "#647084" : "#F8FAFC");
        SetBrush(resources, "Brush.CardHover", isDark ? "#748199" : "#EEF2F6");
        SetBrush(resources, "Brush.PrimaryText", isDark ? "#F5F7FA" : "#1A1D22");
        SetBrush(resources, "Brush.SecondaryText", isDark ? "#CCD4E0" : "#646B76");
        SetBrush(resources, "Brush.SubtleText", isDark ? "#9CA7B8" : "#8B94A3");
        SetBrush(resources, "Brush.Border", isDark ? "#7A879C" : "#D5DCE5");
        SetBrush(resources, "Brush.Accent", isDark ? "#8EA9CE" : "#7394BD");
        SetBrush(resources, "Brush.AccentHover", isDark ? "#A4BDE0" : "#89A8CE");
        SetBrush(resources, "Brush.AccentPressed", isDark ? "#7996BA" : "#6486B0");
        SetBrush(resources, "Brush.AccentSubtle", isDark ? "#5A6F90" : "#E3EBF5");
        SetBrush(resources, "Brush.SoftSurface", isDark ? "#6C778A" : "#EDF1F5");
        SetBrush(resources, "Brush.Success", "#34D399");
        SetBrush(resources, "Brush.Warning", "#FBBF24");
        SetBrush(resources, "Brush.Error", "#F87171");
        SetBrush(resources, "Brush.Info", "#60A5FA");
        SetBrush(resources, "Brush.ScrollTrack", isDark ? "#E1E6ED" : "#EEF2F6");
        SetBrush(resources, "Brush.ScrollThumb", isDark ? "#7A8599" : "#CCD6E2");
        SetBrush(resources, "Brush.ScrollThumbHover", isDark ? "#93A0B5" : "#D7E0EA");
        SetBrush(resources, "Brush.ScrollThumbDrag", isDark ? "#A3AFC2" : "#E1E8F0");

        foreach (Window w in Current.Windows)
            ApplyDarkTitleBar(w, isDark);
    }

    public static void ApplyUiScale(int level)
    {
        level = NormalizeUiScaleLevel(level);
        UserPrefs.UiScaleLevel = level;
        UserPrefs.UiScaleSchemaVersion = CurrentUiScaleSchemaVersion;

        var resources = Current.Resources;
        switch (level)
        {
            case 1:
                SetResource(resources, "Ui.Font.Search", 11d);
                SetResource(resources, "Ui.Font.SectionTitle", 13d);
                SetResource(resources, "Ui.Font.CurrentListTitle", 14d);
                SetResource(resources, "Ui.Font.CurrentListSubtitle", 10d);
                SetResource(resources, "Ui.Font.SidebarName", 11d);
                SetResource(resources, "Ui.Font.SidebarMeta", 9d);
                SetResource(resources, "Ui.Font.ParticipantInitials", 10d);
                SetResource(resources, "Ui.Font.ParticipantName", 11d);
                SetResource(resources, "Ui.Font.QuickAction", 8d);
                SetResource(resources, "Ui.Font.StatusTag", 8d);
                SetResource(resources, "Ui.Font.DetailInitials", 13d);
                SetResource(resources, "Ui.Font.DetailTitle", 16d);
                SetResource(resources, "Ui.Font.DetailMeta", 10d);
                SetResource(resources, "Ui.Font.StatusBar", 10d);
                SetResource(resources, "Ui.Font.Control", 10d);
                SetResource(resources, "Ui.Font.IconAction", 12d);
                SetResource(resources, "Ui.Padding.SecondaryButton", new Thickness(5, 2, 5, 2));
                SetResource(resources, "Ui.Padding.SearchTextBox", new Thickness(8, 5, 32, 5));
                SetResource(resources, "Ui.Padding.SidebarItem", new Thickness(6, 4, 6, 4));
                SetResource(resources, "Ui.Padding.ParticipantItem", new Thickness(6, 4, 6, 4));
                SetResource(resources, "Ui.Padding.WorkingItem", new Thickness(7, 5, 7, 5));
                SetResource(resources, "Ui.Padding.QuickAction", new Thickness(6, 2, 6, 2));
                SetResource(resources, "Ui.Margin.SidebarItemBottom", new Thickness(0, 0, 0, 5));
                SetResource(resources, "Ui.Margin.ParticipantItemBottom", new Thickness(0, 0, 0, 4));
                SetResource(resources, "Ui.Margin.WorkingItemBottom", new Thickness(0, 0, 0, 5));
                SetResource(resources, "Ui.Size.IconButton", 26d);
                SetResource(resources, "Ui.Size.QuickActionMinWidth", 36d);
                SetResource(resources, "Ui.Size.StatusTagHeight", 17d);
                SetResource(resources, "Ui.Size.StatusTagMinWidth", 26d);
                SetResource(resources, "Ui.Size.WorkingButtonMinHeight", 21d);
                SetResource(resources, "Ui.Size.DetailHeaderMinHeight", 88d);
                SetResource(resources, "Ui.Size.DetailTitleMaxHeight", 42d);
                SetResource(resources, "Ui.Size.DetailPhotoCardHeight", 182d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameWidth", 132d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameHeight", 164d);
                SetResource(resources, "Ui.Size.DetailOdooButtonMinWidth", 132d);
                break;
            case 2:
                SetResource(resources, "Ui.Font.Search", 12d);
                SetResource(resources, "Ui.Font.SectionTitle", 14d);
                SetResource(resources, "Ui.Font.CurrentListTitle", 15d);
                SetResource(resources, "Ui.Font.CurrentListSubtitle", 10d);
                SetResource(resources, "Ui.Font.SidebarName", 11d);
                SetResource(resources, "Ui.Font.SidebarMeta", 9d);
                SetResource(resources, "Ui.Font.ParticipantInitials", 10d);
                SetResource(resources, "Ui.Font.ParticipantName", 12d);
                SetResource(resources, "Ui.Font.QuickAction", 8d);
                SetResource(resources, "Ui.Font.StatusTag", 8d);
                SetResource(resources, "Ui.Font.DetailInitials", 14d);
                SetResource(resources, "Ui.Font.DetailTitle", 17d);
                SetResource(resources, "Ui.Font.DetailMeta", 10d);
                SetResource(resources, "Ui.Font.StatusBar", 10d);
                SetResource(resources, "Ui.Font.Control", 10d);
                SetResource(resources, "Ui.Font.IconAction", 13d);
                SetResource(resources, "Ui.Padding.SecondaryButton", new Thickness(6, 2, 6, 2));
                SetResource(resources, "Ui.Padding.SearchTextBox", new Thickness(8, 5, 33, 5));
                SetResource(resources, "Ui.Padding.SidebarItem", new Thickness(6, 5, 6, 5));
                SetResource(resources, "Ui.Padding.ParticipantItem", new Thickness(6, 5, 6, 5));
                SetResource(resources, "Ui.Padding.WorkingItem", new Thickness(7, 5, 7, 5));
                SetResource(resources, "Ui.Padding.QuickAction", new Thickness(6, 2, 6, 2));
                SetResource(resources, "Ui.Margin.SidebarItemBottom", new Thickness(0, 0, 0, 5));
                SetResource(resources, "Ui.Margin.ParticipantItemBottom", new Thickness(0, 0, 0, 4));
                SetResource(resources, "Ui.Margin.WorkingItemBottom", new Thickness(0, 0, 0, 5));
                SetResource(resources, "Ui.Size.IconButton", 27d);
                SetResource(resources, "Ui.Size.QuickActionMinWidth", 38d);
                SetResource(resources, "Ui.Size.StatusTagHeight", 17d);
                SetResource(resources, "Ui.Size.StatusTagMinWidth", 27d);
                SetResource(resources, "Ui.Size.WorkingButtonMinHeight", 21d);
                SetResource(resources, "Ui.Size.DetailHeaderMinHeight", 92d);
                SetResource(resources, "Ui.Size.DetailTitleMaxHeight", 44d);
                SetResource(resources, "Ui.Size.DetailPhotoCardHeight", 190d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameWidth", 136d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameHeight", 170d);
                SetResource(resources, "Ui.Size.DetailOdooButtonMinWidth", 136d);
                break;
            case 3:
                SetResource(resources, "Ui.Font.Search", 13d);
                SetResource(resources, "Ui.Font.SectionTitle", 15d);
                SetResource(resources, "Ui.Font.CurrentListTitle", 16d);
                SetResource(resources, "Ui.Font.CurrentListSubtitle", 11d);
                SetResource(resources, "Ui.Font.SidebarName", 12d);
                SetResource(resources, "Ui.Font.SidebarMeta", 10d);
                SetResource(resources, "Ui.Font.ParticipantInitials", 11d);
                SetResource(resources, "Ui.Font.ParticipantName", 13d);
                SetResource(resources, "Ui.Font.QuickAction", 9d);
                SetResource(resources, "Ui.Font.StatusTag", 9d);
                SetResource(resources, "Ui.Font.DetailInitials", 15d);
                SetResource(resources, "Ui.Font.DetailTitle", 18d);
                SetResource(resources, "Ui.Font.DetailMeta", 11d);
                SetResource(resources, "Ui.Font.StatusBar", 11d);
                SetResource(resources, "Ui.Font.Control", 11d);
                SetResource(resources, "Ui.Font.IconAction", 14d);
                SetResource(resources, "Ui.Padding.SecondaryButton", new Thickness(6, 2, 6, 2));
                SetResource(resources, "Ui.Padding.SearchTextBox", new Thickness(9, 6, 34, 6));
                SetResource(resources, "Ui.Padding.SidebarItem", new Thickness(7, 5, 7, 5));
                SetResource(resources, "Ui.Padding.ParticipantItem", new Thickness(7, 5, 7, 5));
                SetResource(resources, "Ui.Padding.WorkingItem", new Thickness(8, 6, 8, 6));
                SetResource(resources, "Ui.Padding.QuickAction", new Thickness(7, 2, 7, 2));
                SetResource(resources, "Ui.Margin.SidebarItemBottom", new Thickness(0, 0, 0, 6));
                SetResource(resources, "Ui.Margin.ParticipantItemBottom", new Thickness(0, 0, 0, 5));
                SetResource(resources, "Ui.Margin.WorkingItemBottom", new Thickness(0, 0, 0, 6));
                SetResource(resources, "Ui.Size.IconButton", 28d);
                SetResource(resources, "Ui.Size.QuickActionMinWidth", 40d);
                SetResource(resources, "Ui.Size.StatusTagHeight", 18d);
                SetResource(resources, "Ui.Size.StatusTagMinWidth", 28d);
                SetResource(resources, "Ui.Size.WorkingButtonMinHeight", 22d);
                SetResource(resources, "Ui.Size.DetailHeaderMinHeight", 96d);
                SetResource(resources, "Ui.Size.DetailTitleMaxHeight", 46d);
                SetResource(resources, "Ui.Size.DetailPhotoCardHeight", 198d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameWidth", 142d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameHeight", 178d);
                SetResource(resources, "Ui.Size.DetailOdooButtonMinWidth", 140d);
                break;
            case 4:
                SetResource(resources, "Ui.Font.Search", 14d);
                SetResource(resources, "Ui.Font.SectionTitle", 16d);
                SetResource(resources, "Ui.Font.CurrentListTitle", 18d);
                SetResource(resources, "Ui.Font.CurrentListSubtitle", 12d);
                SetResource(resources, "Ui.Font.SidebarName", 13d);
                SetResource(resources, "Ui.Font.SidebarMeta", 11d);
                SetResource(resources, "Ui.Font.ParticipantInitials", 12d);
                SetResource(resources, "Ui.Font.ParticipantName", 14d);
                SetResource(resources, "Ui.Font.QuickAction", 10d);
                SetResource(resources, "Ui.Font.StatusTag", 10d);
                SetResource(resources, "Ui.Font.DetailInitials", 16d);
                SetResource(resources, "Ui.Font.DetailTitle", 20d);
                SetResource(resources, "Ui.Font.DetailMeta", 12d);
                SetResource(resources, "Ui.Font.StatusBar", 12d);
                SetResource(resources, "Ui.Font.Control", 12d);
                SetResource(resources, "Ui.Font.IconAction", 15d);
                SetResource(resources, "Ui.Padding.SecondaryButton", new Thickness(7, 3, 7, 3));
                SetResource(resources, "Ui.Padding.SearchTextBox", new Thickness(10, 8, 36, 8));
                SetResource(resources, "Ui.Padding.SidebarItem", new Thickness(8, 6, 8, 6));
                SetResource(resources, "Ui.Padding.ParticipantItem", new Thickness(8, 6, 8, 6));
                SetResource(resources, "Ui.Padding.WorkingItem", new Thickness(10, 8, 10, 8));
                SetResource(resources, "Ui.Padding.QuickAction", new Thickness(8, 3, 8, 3));
                SetResource(resources, "Ui.Margin.SidebarItemBottom", new Thickness(0, 0, 0, 8));
                SetResource(resources, "Ui.Margin.ParticipantItemBottom", new Thickness(0, 0, 0, 6));
                SetResource(resources, "Ui.Margin.WorkingItemBottom", new Thickness(0, 0, 0, 8));
                SetResource(resources, "Ui.Size.IconButton", 30d);
                SetResource(resources, "Ui.Size.QuickActionMinWidth", 42d);
                SetResource(resources, "Ui.Size.StatusTagHeight", 20d);
                SetResource(resources, "Ui.Size.StatusTagMinWidth", 30d);
                SetResource(resources, "Ui.Size.WorkingButtonMinHeight", 24d);
                SetResource(resources, "Ui.Size.DetailHeaderMinHeight", 108d);
                SetResource(resources, "Ui.Size.DetailTitleMaxHeight", 52d);
                SetResource(resources, "Ui.Size.DetailPhotoCardHeight", 214d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameWidth", 152d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameHeight", 190d);
                SetResource(resources, "Ui.Size.DetailOdooButtonMinWidth", 148d);
                break;
            case 5:
                SetResource(resources, "Ui.Font.Search", 15d);
                SetResource(resources, "Ui.Font.SectionTitle", 17d);
                SetResource(resources, "Ui.Font.CurrentListTitle", 19d);
                SetResource(resources, "Ui.Font.CurrentListSubtitle", 13d);
                SetResource(resources, "Ui.Font.SidebarName", 14d);
                SetResource(resources, "Ui.Font.SidebarMeta", 12d);
                SetResource(resources, "Ui.Font.ParticipantInitials", 13d);
                SetResource(resources, "Ui.Font.ParticipantName", 15d);
                SetResource(resources, "Ui.Font.QuickAction", 11d);
                SetResource(resources, "Ui.Font.StatusTag", 11d);
                SetResource(resources, "Ui.Font.DetailInitials", 17d);
                SetResource(resources, "Ui.Font.DetailTitle", 22d);
                SetResource(resources, "Ui.Font.DetailMeta", 13d);
                SetResource(resources, "Ui.Font.StatusBar", 13d);
                SetResource(resources, "Ui.Font.Control", 13d);
                SetResource(resources, "Ui.Font.IconAction", 16d);
                SetResource(resources, "Ui.Padding.SecondaryButton", new Thickness(8, 4, 8, 4));
                SetResource(resources, "Ui.Padding.SearchTextBox", new Thickness(11, 9, 38, 9));
                SetResource(resources, "Ui.Padding.SidebarItem", new Thickness(9, 7, 9, 7));
                SetResource(resources, "Ui.Padding.ParticipantItem", new Thickness(9, 7, 9, 7));
                SetResource(resources, "Ui.Padding.WorkingItem", new Thickness(11, 9, 11, 9));
                SetResource(resources, "Ui.Padding.QuickAction", new Thickness(9, 4, 9, 4));
                SetResource(resources, "Ui.Margin.SidebarItemBottom", new Thickness(0, 0, 0, 10));
                SetResource(resources, "Ui.Margin.ParticipantItemBottom", new Thickness(0, 0, 0, 8));
                SetResource(resources, "Ui.Margin.WorkingItemBottom", new Thickness(0, 0, 0, 10));
                SetResource(resources, "Ui.Size.IconButton", 32d);
                SetResource(resources, "Ui.Size.QuickActionMinWidth", 46d);
                SetResource(resources, "Ui.Size.StatusTagHeight", 22d);
                SetResource(resources, "Ui.Size.StatusTagMinWidth", 32d);
                SetResource(resources, "Ui.Size.WorkingButtonMinHeight", 26d);
                SetResource(resources, "Ui.Size.DetailHeaderMinHeight", 120d);
                SetResource(resources, "Ui.Size.DetailTitleMaxHeight", 58d);
                SetResource(resources, "Ui.Size.DetailPhotoCardHeight", 232d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameWidth", 164d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameHeight", 205d);
                SetResource(resources, "Ui.Size.DetailOdooButtonMinWidth", 156d);
                break;
            default:
                goto case 5;
        }
    }

    private static void NormalizeUserPrefs(UserPrefs prefs, bool hasExistingPrefs)
    {
        prefs.SelectedListId ??= string.Empty;
        prefs.PreferredWordMonitorId = string.IsNullOrWhiteSpace(prefs.PreferredWordMonitorId)
            ? "__PRIMARY__"
            : prefs.PreferredWordMonitorId;

        if (prefs.UiScaleSchemaVersion < CurrentUiScaleSchemaVersion)
        {
            prefs.UiScaleLevel = hasExistingPrefs
                ? MigrateLegacyUiScaleLevel(prefs.UiScaleLevel)
                : DefaultUiScaleLevel;
            prefs.UiScaleSchemaVersion = CurrentUiScaleSchemaVersion;
        }

        prefs.UiScaleLevel = NormalizeUiScaleLevel(prefs.UiScaleLevel);
    }

    private static int MigrateLegacyUiScaleLevel(int legacyLevel)
    {
        legacyLevel = Math.Clamp(legacyLevel, 1, 4);
        return legacyLevel switch
        {
            1 => 3,
            2 => 4,
            _ => 5
        };
    }

    private static void NormalizeConfig(AppConfig config)
    {
        config.ServerBasePath ??= string.Empty;
        config.SecondaryServerBasePath ??= string.Empty;
        config.LvBasePath ??= string.Empty;
        config.LbBasePath ??= string.Empty;
        config.StartBasePath ??= string.Empty;
        config.ExitBasePath ??= string.Empty;
        config.ScheduleRootPath ??= string.Empty;
        config.WordLbBookmarkName ??= string.Empty;
        config.WordEntryBuBookmarkName ??= string.Empty;
        config.WordEntryBiBookmarkName ??= string.Empty;
        config.VisibleQuickActions ??= QuickActionKeys.CreateDefaults().ToList();

        if (string.IsNullOrWhiteSpace(config.LvBasePath) && !string.IsNullOrWhiteSpace(config.ServerBasePath))
        {
            config.LvBasePath = config.ServerBasePath.Trim();
        }

        config.ServerBasePath = string.IsNullOrWhiteSpace(config.ServerBasePath)
            ? config.LvBasePath.Trim()
            : config.ServerBasePath.Trim();
        config.LvBasePath = config.LvBasePath.Trim();
        config.LbBasePath = config.LbBasePath.Trim();
        config.StartBasePath = config.StartBasePath.Trim();
        config.ExitBasePath = config.ExitBasePath.Trim();
        config.ScheduleRootPath = config.ScheduleRootPath.Trim();

        config.VerlaufsakteKeyword = string.IsNullOrWhiteSpace(config.VerlaufsakteKeyword)
            ? "Verlaufsakte"
            : config.VerlaufsakteKeyword.Trim();
        config.WordBuBookmarkName = string.IsNullOrWhiteSpace(config.WordBuBookmarkName)
            ? "_Bildung"
            : config.WordBuBookmarkName.Trim();
        config.WordBiBookmarkName = string.IsNullOrWhiteSpace(config.WordBiBookmarkName)
            ? "_Berufsintegration"
            : config.WordBiBookmarkName.Trim();
        config.WordBeBookmarkName = string.IsNullOrWhiteSpace(config.WordBeBookmarkName)
            ? "_Beratung"
            : config.WordBeBookmarkName.Trim();
        config.WordLbBookmarkName = string.IsNullOrWhiteSpace(config.WordLbBookmarkName)
            ? "_Lehrbetrieb"
            : config.WordLbBookmarkName.Trim();
        config.WordEntryBuBookmarkName = string.IsNullOrWhiteSpace(config.WordEntryBuBookmarkName)
            ? "_Eintrag_Bildung"
            : config.WordEntryBuBookmarkName.Trim();
        config.WordEntryBiBookmarkName = string.IsNullOrWhiteSpace(config.WordEntryBiBookmarkName)
            ? "_Eintrag_Berufsintegration"
            : config.WordEntryBiBookmarkName.Trim();
        config.VisibleQuickActions = config.VisibleQuickActions
            .Where(key => QuickActionKeys.All.Any(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (config.VisibleQuickActions.Count == 0)
        {
            config.VisibleQuickActions = QuickActionKeys.CreateDefaults().ToList();
        }
        if (config.AutoRefreshHours < 0)
        {
            config.AutoRefreshHours = 0;
        }
    }

    private static void TryPersistNormalizedConfig(AppConfigService configService)
    {
        try
        {
            configService.Save(Config);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Normalisierte Konfiguration konnte beim Start nicht gespeichert werden: {ex.Message}");
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, string colorHex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        resources[key] = brush;
    }

    private static void SetResource(ResourceDictionary resources, string key, object value)
    {
        resources[key] = value;
    }

    private static void RegisterGlobalExceptionLogging()
    {
        Current.DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error("Application.DispatcherUnhandledException", args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            AppLogger.Error($"AppDomain.UnhandledException (IsTerminating={args.IsTerminating})", args.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }

    private static void TryPersistNormalizedUserPrefs(UserPrefsService prefsService)
    {
        try
        {
            prefsService.Save(UserPrefs);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Normalisierte Benutzereinstellungen konnten beim Start nicht gespeichert werden: {ex.Message}");
        }
    }

    private static string ResolveDisplayVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private bool TryAcquireSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
            if (createdNew)
            {
                _ownsSingleInstanceMutex = true;
                return true;
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            return false;
        }
        catch (AbandonedMutexException)
        {
            _ownsSingleInstanceMutex = true;
            return true;
        }
    }

    private void StartSingleInstanceListener()
    {
        _singleInstanceListenerCancellation = new CancellationTokenSource();
        _singleInstanceListenerTask = ListenForSingleInstanceSignalsAsync(_singleInstanceListenerCancellation.Token);
    }

    private async Task ListenForSingleInstanceSignalsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    SingleInstancePipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
                var message = await reader.ReadLineAsync();
                if (string.Equals(message, "ACTIVATE", StringComparison.Ordinal))
                {
                    await Dispatcher.InvokeAsync(BringMainWindowToFront);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Single-Instance-Aktivierung konnte nicht verarbeitet werden: {ex.Message}");
                await Task.Delay(250, cancellationToken);
            }
        }
    }

    private static void SignalRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", SingleInstancePipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            client.Connect(500);
            using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
            writer.WriteLine("ACTIVATE");
        }
        catch
        {
            // If signaling fails, we still suppress the second instance.
        }
    }

    private static void BringMainWindowToFront()
    {
        if (Current?.MainWindow is not Window window)
        {
            return;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        window.Focus();

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(hwnd, IsIconic(hwnd) ? SwRestore : SwShow);

        var wasTopmost = window.Topmost;
        window.Topmost = true;
        window.Topmost = wasTopmost;
        SetForegroundWindow(hwnd);
    }
}









