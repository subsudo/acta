namespace XHub.Models;

public class AttendanceImportResult
{
    public SavedList ImportedList { get; set; } = new();
    public int ParsedLineCount { get; set; }
    public int MatchedCount { get; set; }
    public List<string> UnmatchedLines { get; set; } = new();
}
