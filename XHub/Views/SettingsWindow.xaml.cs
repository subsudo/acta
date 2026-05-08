using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using XHub.Models;
using XHub.Services;

namespace XHub.Views;

public partial class SettingsWindow : Window
{
    private readonly bool _initialIsDarkTheme;
    private readonly bool _showStatusTags;
    private readonly bool _showParticipantPhoto;
    private readonly bool _showMiniSchedule;
    private readonly bool _showNotesPanel;
    private bool _persistThemeChange;
    private SettingsWindowAction _requestedAction = SettingsWindowAction.Save;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        App.ApplyDarkTitleBar(this, App.UserPrefs.IsDarkTheme);
    }

    public SettingsWindow(AppConfig config, UserPrefs prefs)
    {
        InitializeComponent();
        Loaded += SettingsWindow_OnLoaded;

        _initialIsDarkTheme = prefs.IsDarkTheme;
        _showStatusTags = config.ShowStatusTags;
        _showParticipantPhoto = config.ShowParticipantPhoto;
        _showMiniSchedule = prefs.ShowMiniSchedule;
        _showNotesPanel = !prefs.IsNotesPanelCollapsed;

        ServerPathTextBox.Text = string.IsNullOrWhiteSpace(config.LvBasePath)
            ? config.ServerBasePath
            : config.LvBasePath;
        LbPathTextBox.Text = config.LbBasePath;
        StartPathTextBox.Text = config.StartBasePath;
        ExitPathTextBox.Text = config.ExitBasePath;
        SchedulePathTextBox.Text = config.ScheduleRootPath;

        ThemeToggleCheckBox.IsChecked = prefs.IsDarkTheme;

        FolderActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Folder, StringComparer.OrdinalIgnoreCase);
        DocumentActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Document, StringComparer.OrdinalIgnoreCase);
        BuActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Bu, StringComparer.OrdinalIgnoreCase);
        BiActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Bi, StringComparer.OrdinalIgnoreCase);
        BeActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Be, StringComparer.OrdinalIgnoreCase);
        LbActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Lb, StringComparer.OrdinalIgnoreCase);
        EntryBuActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.EntryBu, StringComparer.OrdinalIgnoreCase);
        EntryBiActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.EntryBi, StringComparer.OrdinalIgnoreCase);
        EntryBeActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.EntryBe, StringComparer.OrdinalIgnoreCase);
        EntryLbActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.EntryLb, StringComparer.OrdinalIgnoreCase);
        AutoPrefillEntryCheckBox.IsChecked = prefs.AutoPrefillOnEmptyClipboard;
        DefaultEntryInitialsTextBox.Text = prefs.DefaultEntryInitials ?? string.Empty;

        UpdateThemePreview();
    }

    private void SettingsWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        var maxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 24);
        MaxHeight = maxHeight;
        if (Height > maxHeight)
        {
            Height = maxHeight;
        }
    }

    public SettingsWindowResult Result => new()
    {
        ServerPath = ServerPathTextBox.Text.Trim(),
        LvPath = ServerPathTextBox.Text.Trim(),
        LbPath = LbPathTextBox.Text.Trim(),
        StartPath = StartPathTextBox.Text.Trim(),
        ExitPath = ExitPathTextBox.Text.Trim(),
        SchedulePath = SchedulePathTextBox.Text.Trim(),
        IsDarkTheme = ThemeToggleCheckBox.IsChecked == true,
        ShowStatusTags = _showStatusTags,
        ShowParticipantPhoto = _showParticipantPhoto,
        ShowMiniSchedule = _showMiniSchedule,
        ShowNotesPanel = _showNotesPanel,
        VisibleQuickActions = GetSelectedQuickActions(),
        AutoPrefillOnEmptyClipboard = AutoPrefillEntryCheckBox.IsChecked == true,
        DefaultEntryInitials = DefaultEntryInitialsTextBox.Text.Trim(),
        RequestedAction = _requestedAction
    };

    protected override void OnClosed(EventArgs e)
    {
        if (!_persistThemeChange)
        {
            App.ApplyTheme(_initialIsDarkTheme);
        }

        base.OnClosed(e);
    }

    private List<string> GetSelectedQuickActions()
    {
        var actions = new List<string>();
        if (FolderActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.Folder);
        if (DocumentActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.Document);
        if (BuActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.Bu);
        if (BiActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.Bi);
        if (BeActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.Be);
        if (LbActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.Lb);
        if (EntryBuActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.EntryBu);
        if (EntryBiActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.EntryBi);
        if (EntryBeActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.EntryBe);
        if (EntryLbActionCheckBox.IsChecked == true) actions.Add(QuickActionKeys.EntryLb);
        return actions;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        _persistThemeChange = true;
        _requestedAction = SettingsWindowAction.Save;
        DialogResult = true;
    }

    private void ExportButton_OnClick(object sender, RoutedEventArgs e)
    {
        _requestedAction = SettingsWindowAction.Export;
        DialogResult = true;
    }

    private void ImportButton_OnClick(object sender, RoutedEventArgs e)
    {
        _requestedAction = SettingsWindowAction.Import;
        DialogResult = true;
    }

    private void OpenLogFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppLogger.LogDirectoryPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = AppLogger.LogDirectoryPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Der Log-Ordner konnte nicht geöffnet werden:\n{ex.Message}",
                "Acta",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ThemeToggleCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateThemePreview();
    }

    private void UpdateThemePreview()
    {
        var isDark = ThemeToggleCheckBox.IsChecked == true;
        ThemeModeTextBlock.Text = isDark ? "Dunkel" : "Hell";
        App.ApplyTheme(isDark);
    }

}

public class SettingsWindowResult
{
    public string ServerPath { get; set; } = string.Empty;
    public string LvPath { get; set; } = string.Empty;
    public string LbPath { get; set; } = string.Empty;
    public string StartPath { get; set; } = string.Empty;
    public string ExitPath { get; set; } = string.Empty;
    public string SchedulePath { get; set; } = string.Empty;
    public bool IsDarkTheme { get; set; }
    public bool ShowStatusTags { get; set; } = true;
    public bool ShowParticipantPhoto { get; set; } = true;
    public bool ShowMiniSchedule { get; set; } = true;
    public bool ShowNotesPanel { get; set; }
    public List<string> VisibleQuickActions { get; set; } = QuickActionKeys.CreateDefaults().ToList();
    public bool AutoPrefillOnEmptyClipboard { get; set; }
    public string DefaultEntryInitials { get; set; } = string.Empty;
    public SettingsWindowAction RequestedAction { get; set; } = SettingsWindowAction.Save;
}

public enum SettingsWindowAction { Save, Export, Import }
