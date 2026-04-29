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

        _initialIsDarkTheme = prefs.IsDarkTheme;

        ServerPathTextBox.Text = string.IsNullOrWhiteSpace(config.LvBasePath)
            ? config.ServerBasePath
            : config.LvBasePath;
        LbPathTextBox.Text = config.LbBasePath;
        StartPathTextBox.Text = config.StartBasePath;
        ExitPathTextBox.Text = config.ExitBasePath;
        SchedulePathTextBox.Text = config.ScheduleRootPath;

        ThemeToggleCheckBox.IsChecked = prefs.IsDarkTheme;
        StatusTagsToggleCheckBox.IsChecked = config.ShowStatusTags;
        ParticipantPhotoToggleCheckBox.IsChecked = config.ShowParticipantPhoto;
        MiniScheduleToggleCheckBox.IsChecked = prefs.ShowMiniSchedule;
        NotesPanelToggleCheckBox.IsChecked = !prefs.IsNotesPanelCollapsed;

        FolderActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Folder, StringComparer.OrdinalIgnoreCase);
        DocumentActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Document, StringComparer.OrdinalIgnoreCase);
        BuActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Bu, StringComparer.OrdinalIgnoreCase);
        BiActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Bi, StringComparer.OrdinalIgnoreCase);
        BeActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Be, StringComparer.OrdinalIgnoreCase);
        LbActionCheckBox.IsChecked = config.VisibleQuickActions.Contains(QuickActionKeys.Lb, StringComparer.OrdinalIgnoreCase);

        SetComboSelection(config.AutoRefreshHours);
        UpdateThemePreview();
        UpdateStatusTagsPreview();
        UpdateParticipantPhotoPreview();
        UpdateMiniSchedulePreview();
        UpdateNotesPanelPreview();
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
        ShowStatusTags = StatusTagsToggleCheckBox.IsChecked == true,
        ShowParticipantPhoto = ParticipantPhotoToggleCheckBox.IsChecked == true,
        ShowMiniSchedule = MiniScheduleToggleCheckBox.IsChecked == true,
        ShowNotesPanel = NotesPanelToggleCheckBox.IsChecked == true,
        AutoRefreshHours = GetSelectedRefreshHours(),
        VisibleQuickActions = GetSelectedQuickActions(),
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

    private void StatusTagsToggleCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateStatusTagsPreview();
    }

    private void ParticipantPhotoToggleCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateParticipantPhotoPreview();
    }

    private void MiniScheduleToggleCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateMiniSchedulePreview();
    }

    private void NotesPanelToggleCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        UpdateNotesPanelPreview();
    }

    private void UpdateThemePreview()
    {
        var isDark = ThemeToggleCheckBox.IsChecked == true;
        ThemeModeTextBlock.Text = isDark ? "Dunkel" : "Hell";
        App.ApplyTheme(isDark);
    }

    private void UpdateStatusTagsPreview()
    {
        StatusTagsModeTextBlock.Text = StatusTagsToggleCheckBox.IsChecked == true ? "Ein" : "Aus";
    }

    private void UpdateParticipantPhotoPreview()
    {
        ParticipantPhotoModeTextBlock.Text = ParticipantPhotoToggleCheckBox.IsChecked == true ? "Ein" : "Aus";
    }

    private void UpdateMiniSchedulePreview()
    {
        MiniScheduleModeTextBlock.Text = MiniScheduleToggleCheckBox.IsChecked == true ? "Ein" : "Aus";
    }

    private void UpdateNotesPanelPreview()
    {
        NotesPanelModeTextBlock.Text = NotesPanelToggleCheckBox.IsChecked == true ? "Ein" : "Aus";
    }

    private void SetComboSelection(int hours)
    {
        foreach (var item in AutoRefreshComboBox.Items.OfType<ComboBoxItem>())
        {
            if (int.TryParse(item.Tag?.ToString(), out var value) && value == hours)
            {
                AutoRefreshComboBox.SelectedItem = item;
                return;
            }
        }

        AutoRefreshComboBox.SelectedIndex = 0;
    }

    private int GetSelectedRefreshHours()
    {
        return AutoRefreshComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var value)
            ? value
            : 0;
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
    public int AutoRefreshHours { get; set; }
    public List<string> VisibleQuickActions { get; set; } = QuickActionKeys.CreateDefaults().ToList();
    public SettingsWindowAction RequestedAction { get; set; } = SettingsWindowAction.Save;
}

public enum SettingsWindowAction { Save, Export, Import }
