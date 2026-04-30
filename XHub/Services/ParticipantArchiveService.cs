using System.IO;
using XHub.Models;

namespace XHub.Services;

public sealed class ParticipantArchiveService
{
    public const string ActiveExitFolderName = "031_im Austritt";
    private const string ArchiveStatusTag = "Archiv";

    public static string? TryGetArchiveRoot(string exitBasePath)
    {
        if (string.IsNullOrWhiteSpace(exitBasePath))
        {
            return null;
        }

        try
        {
            var activeExitPath = Path.Combine(exitBasePath, ActiveExitFolderName);
            return Directory.Exists(activeExitPath) ? exitBasePath : null;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Archivmodus konnte nicht geprüft werden '{exitBasePath}': {ex.Message}");
            return null;
        }
    }

    public static string GetIndexableExitPath(string exitBasePath)
    {
        var archiveRoot = TryGetArchiveRoot(exitBasePath);
        return archiveRoot is null ? exitBasePath : Path.Combine(archiveRoot, ActiveExitFolderName);
    }

    public static bool IsArchivedParticipantPath(string participantPath, string? archiveRootPath)
    {
        if (string.IsNullOrWhiteSpace(participantPath) || string.IsNullOrWhiteSpace(archiveRootPath))
        {
            return false;
        }

        try
        {
            var parent = Directory.GetParent(participantPath);
            if (parent is null || !IsArchiveLetterFolder(parent.Name))
            {
                return false;
            }

            var parentRoot = Directory.GetParent(parent.FullName);
            return parentRoot is not null &&
                   string.Equals(
                       Path.GetFullPath(parentRoot.FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                       Path.GetFullPath(archiveRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                       StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public Task<IndexBuildResult> BuildArchiveAsync(string archiveRootPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => BuildArchive(archiveRootPath, cancellationToken), cancellationToken);
    }

    private static IndexBuildResult BuildArchive(string archiveRootPath, CancellationToken cancellationToken)
    {
        var entries = new List<ParticipantIndexEntry>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(archiveRootPath) || !Directory.Exists(archiveRootPath))
        {
            warnings.Add($"Archivpfad nicht erreichbar: {archiveRootPath}");
            return new IndexBuildResult { Entries = entries, Warnings = warnings };
        }

        foreach (var letter in Enumerable.Range('A', 26).Select(value => ((char)value).ToString()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var letterPath = Path.Combine(archiveRootPath, letter);
            if (!Directory.Exists(letterPath))
            {
                continue;
            }

            try
            {
                foreach (var participantDirectory in Directory.GetDirectories(letterPath, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    entries.Add(CreateArchiveEntry(participantDirectory));
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Archivordner konnte nicht gelesen werden: {letterPath}");
                AppLogger.Warn($"Archivordner konnte nicht gelesen werden '{letterPath}': {ex.Message}");
            }
        }

        return new IndexBuildResult
        {
            Entries = entries
                .DistinctBy(entry => entry.ParticipantKey, StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            Warnings = warnings
        };
    }

    public static ParticipantIndexEntry CreateArchiveFallbackEntry(string participantPath)
    {
        return CreateArchiveEntry(participantPath);
    }

    private static ParticipantIndexEntry CreateArchiveEntry(string participantPath)
    {
        var folderName = Path.GetFileName(participantPath);
        var baseTokens = SearchTextUtility.Tokenize(folderName).ToList();
        var fallbackTokens = SearchTextUtility.Tokenize(SearchTextUtility.ReplaceUmlauts(folderName)).ToList();

        return new ParticipantIndexEntry
        {
            ParticipantKey = participantPath,
            DisplayName = folderName,
            FolderPath = participantPath,
            DocumentPath = string.Empty,
            ImagePath = TryFindPhoto(participantPath) ?? string.Empty,
            SourceLabel = ArchiveStatusTag,
            StatusTag = ArchiveStatusTag,
            IsArchived = true,
            SearchTokens = baseTokens,
            SearchTokensFallback = fallbackTokens
        };
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
            AppLogger.Warn($"Archivfoto konnte nicht geprüft werden '{folderPath}': {ex.Message}");
            return null;
        }
    }

    private static bool IsArchiveLetterFolder(string folderName)
    {
        return folderName.Length == 1 && folderName[0] is >= 'A' and <= 'Z';
    }
}
