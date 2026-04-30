using System.IO;
using XHub.Models;

namespace XHub.Services;

public class ParticipantIndexService
{
    private const string StartInterviewFolderName = "012_Erstgespräch";
    private const string StartGroupedFolderName = "013_start";

    private readonly AppConfig _config;
    private readonly InitialsResolver _initialsResolver;
    private readonly object _sync = new();
    private IReadOnlyList<ParticipantIndexEntry> _entries = Array.Empty<ParticipantIndexEntry>();

    public ParticipantIndexService(AppConfig config, InitialsResolver initialsResolver)
    {
        _config = config;
        _initialsResolver = initialsResolver;
    }

    public IReadOnlyList<ParticipantIndexEntry> GetSnapshot()
    {
        lock (_sync)
        {
            return _entries.ToList();
        }
    }

    public async Task<IndexBuildResult> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var result = await Task.Run(() => BuildIndex(cancellationToken), cancellationToken);
        lock (_sync)
        {
            _entries = result.Entries;
        }

        return result;
    }

    private IndexBuildResult BuildIndex(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var directories = new List<(string Path, string SourceLabel)>();
        var lvBasePath = string.IsNullOrWhiteSpace(_config.LvBasePath)
            ? _config.ServerBasePath
            : _config.LvBasePath;

        CollectDirectories(lvBasePath, "LV", _config.VerlaufsakteKeyword, directories, warnings);
        if (_config.UseSecondaryServerBasePath)
        {
            CollectDirectories(_config.SecondaryServerBasePath, "LV", _config.VerlaufsakteKeyword, directories, warnings);
        }

        CollectDirectories(_config.LbBasePath, "LB", _config.VerlaufsakteKeyword, directories, warnings);
        CollectDirectories(_config.StartBasePath, "ST", _config.VerlaufsakteKeyword, directories, warnings);
        CollectDirectories(ParticipantArchiveService.GetIndexableExitPath(_config.ExitBasePath), "AU", _config.VerlaufsakteKeyword, directories, warnings);

        var entries = new List<ParticipantIndexEntry>();
        foreach (var item in directories.DistinctBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderName = Path.GetFileName(item.Path);
            var docPath = TryFindDocument(item.Path, _config.VerlaufsakteKeyword);
            var initials = _initialsResolver.TryResolveFromDocumentPath(docPath);
            var baseTokens = SearchTextUtility.Tokenize(folderName).ToList();
            var fallbackTokens = SearchTextUtility.Tokenize(SearchTextUtility.ReplaceUmlauts(folderName)).ToList();
            if (!string.IsNullOrWhiteSpace(initials))
            {
                baseTokens.Add(initials.ToLowerInvariant());
                fallbackTokens.Add(SearchTextUtility.ReplaceUmlauts(initials).ToLowerInvariant());
            }

            entries.Add(new ParticipantIndexEntry
            {
                ParticipantKey = item.Path,
                DisplayName = folderName,
                FolderPath = item.Path,
                DocumentPath = docPath ?? string.Empty,
                Initials = initials,
                ImagePath = TryFindPhoto(item.Path) ?? string.Empty,
                SourceLabel = item.SourceLabel,
                StatusTag = item.SourceLabel,
                SearchTokens = baseTokens,
                SearchTokensFallback = fallbackTokens
            });
        }

        return new IndexBuildResult
        {
            Entries = entries.OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList(),
            Warnings = warnings
        };
    }

    private static void CollectDirectories(string basePath, string label, string keyword, List<(string Path, string SourceLabel)> target, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return;
        }

        if (!Directory.Exists(basePath))
        {
            warnings.Add($"Pfad nicht erreichbar: {basePath}");
            AppLogger.Warn($"XHub-Index: Pfad nicht erreichbar: '{basePath}'.");
            return;
        }

        try
        {
            if (string.Equals(label, "ST", StringComparison.OrdinalIgnoreCase))
            {
                CollectStartDirectories(basePath, label, keyword, target, warnings);
                return;
            }

            foreach (var directory in Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly))
            {
                if (!ShouldIndexDirectory(directory, label))
                {
                    continue;
                }

                target.Add((directory, label));
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Ordner konnten nicht gelesen werden: {basePath}");
            AppLogger.Warn($"XHub-Index: Ordner konnten nicht gelesen werden '{basePath}': {ex.Message}");
        }
    }

    private static void CollectStartDirectories(
        string basePath,
        string label,
        string keyword,
        List<(string Path, string SourceLabel)> target,
        List<string> warnings)
    {
        var groupedStartPath = Path.Combine(basePath, StartGroupedFolderName);
        var interviewPath = Path.Combine(basePath, StartInterviewFolderName);
        var hasGroupedStartPath = Directory.Exists(groupedStartPath);
        var hasInterviewPath = Directory.Exists(interviewPath);

        if (hasGroupedStartPath || hasInterviewPath)
        {
            if (hasInterviewPath)
            {
                AddDirectParticipantDirectories(interviewPath, label, keyword, target, warnings);
            }

            if (hasGroupedStartPath)
            {
                AddNestedParticipantDirectories(groupedStartPath, label, keyword, target, warnings);
            }

            return;
        }

        var directCount = AddDirectParticipantDirectories(basePath, label, keyword, target, warnings);
        if (directCount > 0)
        {
            return;
        }

        AddNestedParticipantDirectories(basePath, label, keyword, target, warnings);
    }

    private static int AddDirectParticipantDirectories(
        string basePath,
        string label,
        string keyword,
        List<(string Path, string SourceLabel)> target,
        List<string> warnings)
    {
        var count = 0;
        foreach (var participantDirectory in EnumerateTopDirectories(basePath, warnings))
        {
            if (!LooksLikeParticipantDirectory(participantDirectory, label, keyword))
            {
                continue;
            }

            target.Add((participantDirectory, label));
            count++;
        }

        return count;
    }

    private static int AddNestedParticipantDirectories(
        string basePath,
        string label,
        string keyword,
        List<(string Path, string SourceLabel)> target,
        List<string> warnings)
    {
        var count = 0;
        foreach (var groupDirectory in EnumerateTopDirectories(basePath, warnings))
        {
            foreach (var participantDirectory in EnumerateTopDirectories(groupDirectory, warnings))
            {
                if (!LooksLikeParticipantDirectory(participantDirectory, label, keyword))
                {
                    continue;
                }

                target.Add((participantDirectory, label));
                count++;
            }
        }

        return count;
    }

    private static IEnumerable<string> EnumerateTopDirectories(string basePath, List<string> warnings)
    {
        try
        {
            return Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            warnings.Add($"Ordner konnten nicht gelesen werden: {basePath}");
            AppLogger.Warn($"XHub-Index: Ordner konnten nicht gelesen werden '{basePath}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static bool LooksLikeParticipantDirectory(string directoryPath, string label, string keyword)
    {
        if (!ShouldIndexDirectory(directoryPath, label))
        {
            return false;
        }

        return TryFindDocument(directoryPath, keyword) is not null;
    }

    private static bool ShouldIndexDirectory(string directoryPath, string label)
    {
        var folderName = Path.GetFileName(directoryPath);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        if (string.Equals(label, "LV", StringComparison.OrdinalIgnoreCase) &&
            StartsWithExcludedLvPrefix(folderName))
        {
            return false;
        }

        if (string.Equals(label, "LB", StringComparison.OrdinalIgnoreCase) &&
            folderName.StartsWith("_", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool StartsWithExcludedLvPrefix(string folderName)
    {
        return folderName.StartsWith("00", StringComparison.OrdinalIgnoreCase)
               || folderName.StartsWith("01", StringComparison.OrdinalIgnoreCase)
               || folderName.StartsWith("02", StringComparison.OrdinalIgnoreCase)
               || folderName.StartsWith("03", StringComparison.OrdinalIgnoreCase)
               || folderName.StartsWith("05", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryFindPhoto(string folderPath)
    {
        try
        {
            var photoPath = Path.Combine(folderPath, "Admin", "8_Übriges", "foto.jpg");
            return File.Exists(photoPath) ? photoPath : null;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub-Index: Foto konnte nicht geprüft werden '{folderPath}': {ex.Message}");
            return null;
        }
    }

    private static string? TryFindDocument(string folderPath, string keyword)
    {
        try
        {
            return Directory
                .GetFiles(folderPath, "*.docx", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"XHub-Index: Dokumente konnten nicht gelesen werden '{folderPath}': {ex.Message}");
            return null;
        }
    }
}
