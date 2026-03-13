namespace XHub.Models;

public class UserPrefs
{
    public bool IsDarkTheme { get; set; } = true;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public string SelectedListId { get; set; } = string.Empty;
    public bool IsListPanelCollapsed { get; set; }
    public bool IsDetailPanelCollapsed { get; set; } = true;
    public int UiScaleLevel { get; set; } = 2;
    public bool ShowMiniSchedule { get; set; } = true;
    public bool OpenWordMaximized { get; set; }
    public string PreferredWordMonitorId { get; set; } = "__PRIMARY__";
}
