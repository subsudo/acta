namespace XHub.Models;

public class IndexBuildResult
{
    public IReadOnlyList<ParticipantIndexEntry> Entries { get; set; } = Array.Empty<ParticipantIndexEntry>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}
