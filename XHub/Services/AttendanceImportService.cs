using System.Text.RegularExpressions;
using XHub.Models;

namespace XHub.Services;

public class AttendanceImportService
{
    private static readonly Regex InlineStatusRegex = new(@"\s+(Anwesend|Verspätet|Abwesend(?:\s*\([^\)]+\))?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly ParticipantSearchService _searchService;

    public AttendanceImportService(ParticipantSearchService searchService)
    {
        _searchService = searchService;
    }

    public AttendanceImportResult Import(string rawText, IReadOnlyList<ParticipantIndexEntry> entries)
    {
        var result = new AttendanceImportResult
        {
            ImportedList = new SavedList
            {
                Name = $"Temporärer Import {DateTime.Now:dd.MM.yyyy HH:mm}"
            }
        };

        var names = ExtractNames(rawText);
        result.ParsedLineCount = names.Count;

        foreach (var name in names)
        {
            var participant = _searchService.ResolveSingleImportedParticipant(name, entries);
            if (participant is null)
            {
                result.UnmatchedLines.Add(name);
                continue;
            }

            if (result.ImportedList.Items.Any(item => string.Equals(item.ParticipantKey, participant.ParticipantKey, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.ImportedList.Items.Add(new SavedListItem
            {
                ParticipantKey = participant.ParticipantKey,
                SortOrder = result.ImportedList.Items.Count
            });
            result.MatchedCount++;
        }

        return result;
    }

    private static List<string> ExtractNames(string rawText)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return result;
        }

        var lines = rawText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string candidate;
            if (line.Contains('\t'))
            {
                candidate = line.Split('\t')[0].Trim();
            }
            else
            {
                var statusMatch = InlineStatusRegex.Match(line);
                candidate = statusMatch.Success ? line[..statusMatch.Index].Trim() : line;
            }

            if (!string.IsNullOrWhiteSpace(candidate))
            {
                result.Add(candidate);
            }
        }

        return result;
    }
}
