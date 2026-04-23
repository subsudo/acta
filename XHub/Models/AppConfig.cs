namespace XHub.Models;

public class AppConfig
{
    public string ServerBasePath { get; set; } = string.Empty;
    public bool UseSecondaryServerBasePath { get; set; }
    public string SecondaryServerBasePath { get; set; } = string.Empty;
    public string LvBasePath { get; set; } = string.Empty;
    public string LbBasePath { get; set; } = string.Empty;
    public string StartBasePath { get; set; } = string.Empty;
    public string ExitBasePath { get; set; } = string.Empty;
    public string VerlaufsakteKeyword { get; set; } = "Verlaufsakte";
    public string WordBuBookmarkName { get; set; } = "_Bildung";
    public string WordBiBookmarkName { get; set; } = "_Berufsintegration";
    public string WordBeBookmarkName { get; set; } = "_Beratung";
    public string WordLbBookmarkName { get; set; } = "_Lehrbetrieb";
    public List<string> VisibleQuickActions { get; set; } = QuickActionKeys.CreateDefaults().ToList();
    public int AutoRefreshHours { get; set; }
    public bool ShowStatusTags { get; set; } = true;
    public bool ShowParticipantPhoto { get; set; } = true;
    public string ScheduleRootPath { get; set; } = string.Empty;
}
