namespace XHub.Models;

public class ExportPackage
{
    public int Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<SavedList> Lists { get; set; } = new();
}
