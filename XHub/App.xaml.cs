using System.IO;
using System.Runtime.InteropServices;
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

    private const int MinUiScaleLevel = 1;
    private const int MaxUiScaleLevel = 4;

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
    public static AppConfig Config { get; private set; } = new();
    public static UserPrefs UserPrefs { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
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
        var diagnosticsDirectory = ResolveDiagnosticsDirectory(AppDataDirectoryPath);
        WeeklyScheduleDiagnosticsPath = Path.Combine(diagnosticsDirectory, "weekly-schedule-diagnostics.json");
        WeeklyScheduleDiagnosticsBackupPath = Path.Combine(diagnosticsDirectory, "weekly-schedule-diagnostics.bak");

        RegisterGlobalExceptionLogging();

        var configService = new AppConfigService(SettingsPath, SettingsBackupPath, CreateDefaultConfig());
        Config = configService.Load();
        NormalizeConfig(Config);
        TryPersistNormalizedConfig(configService);

        var prefsService = new UserPrefsService(UserPrefsPath, UserPrefsBackupPath);
        UserPrefs = prefsService.Load();
        UserPrefs.UiScaleLevel = NormalizeUiScaleLevel(UserPrefs.UiScaleLevel);
        ApplyTheme(UserPrefs.IsDarkTheme);
        ApplyUiScale(UserPrefs.UiScaleLevel);

        base.OnStartup(e);

        var window = new MainWindow();
        window.Show();
    }

    private static string ResolveDiagnosticsDirectory(string appDataDirectory)
    {
        var localDiagnosticsDirectory = Path.Combine(AppContext.BaseDirectory, "diagnostics");
        if (TryEnsureWritableDirectory(localDiagnosticsDirectory))
        {
            return localDiagnosticsDirectory;
        }

        var appDataDiagnosticsDirectory = Path.Combine(appDataDirectory, "diagnostics");
        Directory.CreateDirectory(appDataDiagnosticsDirectory);
        return appDataDiagnosticsDirectory;
    }

    private static bool TryEnsureWritableDirectory(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            var probePath = Path.Combine(directoryPath, ".write-test.tmp");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public static AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            ServerBasePath = DefaultServerBasePath,
            UseSecondaryServerBasePath = false,
            SecondaryServerBasePath = string.Empty,
            LvBasePath = DefaultServerBasePath,
            LbBasePath = string.Empty,
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
            ScheduleRootPath = string.Empty
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

        SetBrush(resources, "Brush.WindowBg", isDark ? "#262B34" : "#F4F6F8");
        SetBrush(resources, "Brush.PanelBg", isDark ? "#313846" : "#FFFFFF");
        SetBrush(resources, "Brush.CardBg", isDark ? "#3C4556" : "#F8FAFC");
        SetBrush(resources, "Brush.CardHover", isDark ? "#485365" : "#EEF2F6");
        SetBrush(resources, "Brush.PrimaryText", isDark ? "#F5F7FA" : "#1A1D22");
        SetBrush(resources, "Brush.SecondaryText", isDark ? "#CCD4E0" : "#646B76");
        SetBrush(resources, "Brush.SubtleText", isDark ? "#9CA7B8" : "#8B94A3");
        SetBrush(resources, "Brush.Border", isDark ? "#59647A" : "#D5DCE5");
        SetBrush(resources, "Brush.Accent", isDark ? "#8EA9CE" : "#7394BD");
        SetBrush(resources, "Brush.AccentHover", isDark ? "#A4BDE0" : "#89A8CE");
        SetBrush(resources, "Brush.AccentPressed", isDark ? "#7996BA" : "#6486B0");
        SetBrush(resources, "Brush.AccentSubtle", isDark ? "#3D4F6B" : "#E3EBF5");
        SetBrush(resources, "Brush.SoftSurface", isDark ? "#4A5260" : "#EDF1F5");
        SetBrush(resources, "Brush.Success", "#34D399");
        SetBrush(resources, "Brush.Warning", "#FBBF24");
        SetBrush(resources, "Brush.Error", "#F87171");
        SetBrush(resources, "Brush.Info", "#60A5FA");

        foreach (Window w in Current.Windows)
            ApplyDarkTitleBar(w, isDark);
    }

    public static void ApplyUiScale(int level)
    {
        level = NormalizeUiScaleLevel(level);
        UserPrefs.UiScaleLevel = level;

        var resources = Current.Resources;
        switch (level)
        {
            case 1:
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
            case 2:
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
            case 3:
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
                SetResource(resources, "Ui.Font.Search", 16d);
                SetResource(resources, "Ui.Font.SectionTitle", 18d);
                SetResource(resources, "Ui.Font.CurrentListTitle", 21d);
                SetResource(resources, "Ui.Font.CurrentListSubtitle", 14d);
                SetResource(resources, "Ui.Font.SidebarName", 15d);
                SetResource(resources, "Ui.Font.SidebarMeta", 13d);
                SetResource(resources, "Ui.Font.ParticipantInitials", 14d);
                SetResource(resources, "Ui.Font.ParticipantName", 16d);
                SetResource(resources, "Ui.Font.QuickAction", 12d);
                SetResource(resources, "Ui.Font.StatusTag", 12d);
                SetResource(resources, "Ui.Font.DetailInitials", 18d);
                SetResource(resources, "Ui.Font.DetailTitle", 24d);
                SetResource(resources, "Ui.Font.DetailMeta", 14d);
                SetResource(resources, "Ui.Font.StatusBar", 14d);
                SetResource(resources, "Ui.Font.Control", 14d);
                SetResource(resources, "Ui.Font.IconAction", 17d);
                SetResource(resources, "Ui.Padding.SecondaryButton", new Thickness(9, 5, 9, 5));
                SetResource(resources, "Ui.Padding.SearchTextBox", new Thickness(12, 10, 40, 10));
                SetResource(resources, "Ui.Padding.SidebarItem", new Thickness(10, 8, 10, 8));
                SetResource(resources, "Ui.Padding.ParticipantItem", new Thickness(10, 8, 10, 8));
                SetResource(resources, "Ui.Padding.WorkingItem", new Thickness(12, 10, 12, 10));
                SetResource(resources, "Ui.Padding.QuickAction", new Thickness(10, 5, 10, 5));
                SetResource(resources, "Ui.Margin.SidebarItemBottom", new Thickness(0, 0, 0, 12));
                SetResource(resources, "Ui.Margin.ParticipantItemBottom", new Thickness(0, 0, 0, 10));
                SetResource(resources, "Ui.Margin.WorkingItemBottom", new Thickness(0, 0, 0, 12));
                SetResource(resources, "Ui.Size.IconButton", 34d);
                SetResource(resources, "Ui.Size.QuickActionMinWidth", 50d);
                SetResource(resources, "Ui.Size.StatusTagHeight", 24d);
                SetResource(resources, "Ui.Size.StatusTagMinWidth", 34d);
                SetResource(resources, "Ui.Size.WorkingButtonMinHeight", 28d);
                SetResource(resources, "Ui.Size.DetailHeaderMinHeight", 132d);
                SetResource(resources, "Ui.Size.DetailTitleMaxHeight", 64d);
                SetResource(resources, "Ui.Size.DetailPhotoCardHeight", 250d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameWidth", 176d);
                SetResource(resources, "Ui.Size.DetailPhotoFrameHeight", 220d);
                SetResource(resources, "Ui.Size.DetailOdooButtonMinWidth", 168d);
                break;
        }
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
        resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
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
}









