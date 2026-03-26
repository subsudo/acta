using XHub.Models;

namespace XHub.Services;

public class ParticipantSearchService
{
    public IReadOnlyList<ParticipantIndexEntry> Search(string query, IEnumerable<ParticipantIndexEntry> entries, int maxResults = 40)
    {
        var trimmedQuery = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return Array.Empty<ParticipantIndexEntry>();
        }

        var queryTokens = SearchTextUtility.Tokenize(trimmedQuery);
        var fallbackQueryTokens = SearchTextUtility.Tokenize(SearchTextUtility.ReplaceUmlauts(trimmedQuery));
        if (queryTokens.Count == 0 && fallbackQueryTokens.Count == 0)
        {
            return Array.Empty<ParticipantIndexEntry>();
        }

        return entries
            .Select(entry => (Entry: entry, Score: Score(trimmedQuery, queryTokens, fallbackQueryTokens, entry)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxResults)
            .Select(item => item.Entry)
            .ToList();
    }

    public ParticipantIndexEntry? ResolveSingleImportedParticipant(string rawName, IEnumerable<ParticipantIndexEntry> entries)
    {
        var required = SearchTextUtility.BuildTokenCounts(SearchTextUtility.Tokenize(rawName));
        var matches = entries.Where(entry => SearchTextUtility.HasTokenCountsMatch(required, entry.SearchTokens)).ToList();
        if (matches.Count == 1)
        {
            return matches[0];
        }

        required = SearchTextUtility.BuildTokenCounts(SearchTextUtility.Tokenize(SearchTextUtility.ReplaceUmlauts(rawName)));
        matches = entries.Where(entry => SearchTextUtility.HasTokenCountsMatch(required, entry.SearchTokensFallback)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static int Score(
        string rawQuery,
        IReadOnlyList<string> queryTokens,
        IReadOnlyList<string> fallbackQueryTokens,
        ParticipantIndexEntry entry)
    {
        var bestScore = 0;

        if (entry.DisplayName.StartsWith(rawQuery, StringComparison.OrdinalIgnoreCase))
        {
            bestScore = Math.Max(bestScore, 600);
        }

        if (queryTokens.Count > 0 && queryTokens.All(queryToken => entry.SearchTokens.Any(token => token.StartsWith(queryToken, StringComparison.OrdinalIgnoreCase))))
        {
            bestScore = Math.Max(bestScore, 450 + queryTokens.Count);
        }
        else if (fallbackQueryTokens.Count > 0 && fallbackQueryTokens.All(queryToken => entry.SearchTokensFallback.Any(token => token.StartsWith(queryToken, StringComparison.OrdinalIgnoreCase))))
        {
            bestScore = Math.Max(bestScore, 420 + fallbackQueryTokens.Count);
        }
        else if (queryTokens.Count > 0 && queryTokens.All(queryToken => entry.SearchTokens.Any(token => token.Contains(queryToken, StringComparison.OrdinalIgnoreCase))))
        {
            bestScore = Math.Max(bestScore, 280 + queryTokens.Count);
        }
        else if (fallbackQueryTokens.Count > 0 && fallbackQueryTokens.All(queryToken => entry.SearchTokensFallback.Any(token => token.Contains(queryToken, StringComparison.OrdinalIgnoreCase))))
        {
            bestScore = Math.Max(bestScore, 250 + fallbackQueryTokens.Count);
        }

        return bestScore;
    }
}
