using System.Text.RegularExpressions;

namespace XHub.Services;

public static class SearchTextUtility
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return TokenRegex.Matches(value.ToLowerInvariant())
            .Select(match => match.Value)
            .ToArray();
    }

    public static string ReplaceUmlauts(string value)
    {
        return value
            .Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
            .Replace("ß", "ss", StringComparison.OrdinalIgnoreCase);
    }

    public static Dictionary<string, int> BuildTokenCounts(IEnumerable<string> tokens)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens.Where(IsRobustToken))
        {
            result[token] = result.TryGetValue(token, out var count) ? count + 1 : 1;
        }

        return result;
    }

    public static bool HasTokenCountsMatch(Dictionary<string, int> required, IEnumerable<string> candidateTokens)
    {
        if (required.Count == 0)
        {
            return false;
        }

        var available = BuildTokenCounts(candidateTokens);
        foreach (var item in required)
        {
            if (!available.TryGetValue(item.Key, out var count) || count < item.Value)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsRobustToken(string token)
    {
        return !string.IsNullOrWhiteSpace(token) && token.Length >= 2 && token.Any(char.IsLetterOrDigit);
    }
}
