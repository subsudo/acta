namespace XHub.Models;

public class UserPrefs
{
    public bool IsDarkTheme { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public string SelectedListId { get; set; } = string.Empty;
    public bool IsListPanelCollapsed { get; set; }
    public bool IsDetailPanelCollapsed { get; set; } = true;
    public bool IsNotesPanelCollapsed { get; set; } = true;
    public double? NotesPanelWidth { get; set; }
    public bool IsArchiveSearchEnabled { get; set; }
    public int UiScaleLevel { get; set; } = 2;
    public int UiScaleSchemaVersion { get; set; }
    public bool ShowMiniSchedule { get; set; } = true;
    public bool AutoPrefillOnEmptyClipboard { get; set; }
    public string DefaultEntryInitials { get; set; } = string.Empty;
}
