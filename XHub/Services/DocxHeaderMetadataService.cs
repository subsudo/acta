using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

namespace XHub.Services;

public sealed class DocxHeaderMetadataService
{
    // Nur gezielt erhöhen, wenn Struktur oder fachliche Bedeutung der gecachten
    // Header-Werte sich ändert und ein alter Cache falsches Verhalten konservieren würde.
    private const int CacheVersion = 4;

    private static readonly Regex InlineCounselorRegex = new(@"Beratungsperson\s*:?\s*(?<code>(?:[\p{Lu}\p{N}]\s*){1,8})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PlaceholderHeaderRegex = new(@"Nachname|Vorname|Name\s+Vorname|\bXX\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UpperTokenRegex = new(@"^[\p{Lu}\p{N}]{1,8}$", RegexOptions.Compiled);
    private static readonly Regex CounselorInitialsRegex = new(@"^[\p{Lu}\p{N}]{2}$", RegexOptions.Compiled);

    private readonly object _syncRoot = new();
    private readonly string _cachePath;
    private readonly string _cacheBackupPath;
    private readonly Dictionary<string, CacheEntry> _cache;

    public DocxHeaderMetadataService(string cachePath, string cacheBackupPath)
    {
        _cachePath = cachePath;
        _cacheBackupPath = cacheBackupPath;
        _cache = LoadCache();
    }

    public HeaderMetadata Read(string documentPath)
    {
        var fileInfo = new FileInfo(documentPath);
        if (!fileInfo.Exists)
        {
            return HeaderMetadata.Empty;
        }

        CacheEntry? previousCacheEntry = null;
        lock (_syncRoot)
        {
            if (_cache.TryGetValue(documentPath, out var cached) &&
                cached.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc &&
                cached.Length == fileInfo.Length)
            {
                return cached.Metadata;
            }

            _cache.TryGetValue(documentPath, out previousCacheEntry);
        }

        HeaderMetadata metadata;
        try
        {
            metadata = ReadFromPackage(documentPath);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Header-Metadaten konnten nicht gelesen werden '{documentPath}': {ex.Message}");
            if (previousCacheEntry is not null)
            {
                AppLogger.Info($"Verwende vorhandenen Header-Metadaten-Cache weiter '{documentPath}'.");
                return previousCacheEntry.Metadata;
            }

            return HeaderMetadata.Empty;
        }

        UpdateCache(fileInfo, metadata);
        return metadata;
    }

    private Dictionary<string, CacheEntry> LoadCache()
    {
        var document = JsonStorage.Load(_cachePath, _cacheBackupPath, () => new HeaderMetadataCacheDocument());
        if (document.Version != CacheVersion)
        {
            return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        return document.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.DocumentPath))
            .ToDictionary(
                entry => entry.DocumentPath,
                entry => new CacheEntry(
                    entry.LastWriteTimeUtc,
                    entry.Length,
                    new HeaderMetadata(entry.OdooUrl ?? string.Empty, entry.CounselorInitials ?? string.Empty)),
                StringComparer.OrdinalIgnoreCase);
    }

    private void UpdateCache(FileInfo fileInfo, HeaderMetadata metadata)
    {
        HeaderMetadataCacheDocument? documentToPersist = null;
        lock (_syncRoot)
        {
            var path = fileInfo.FullName;
            if (_cache.TryGetValue(path, out var existing) &&
                existing.LastWriteTimeUtc == fileInfo.LastWriteTimeUtc &&
                existing.Length == fileInfo.Length &&
                existing.Metadata == metadata)
            {
                return;
            }

            _cache[path] = new CacheEntry(fileInfo.LastWriteTimeUtc, fileInfo.Length, metadata);
            documentToPersist = CreateCacheDocumentUnsafe();
        }

        PersistCache(documentToPersist);
    }

    private HeaderMetadataCacheDocument CreateCacheDocumentUnsafe()
    {
        return new HeaderMetadataCacheDocument
        {
            Version = CacheVersion,
            Entries = _cache
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new HeaderMetadataCacheEntry
                {
                    DocumentPath = entry.Key,
                    LastWriteTimeUtc = entry.Value.LastWriteTimeUtc,
                    Length = entry.Value.Length,
                    OdooUrl = entry.Value.Metadata.OdooUrl,
                    CounselorInitials = entry.Value.Metadata.CounselorInitials
                })
                .ToList()
        };
    }

    private void PersistCache(HeaderMetadataCacheDocument? document)
    {
        if (document is null)
        {
            return;
        }

        try
        {
            JsonStorage.SaveAtomic(_cachePath, _cacheBackupPath, document);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Header-Metadaten-Cache konnte nicht gespeichert werden: {ex.Message}");
        }
    }

    private static HeaderMetadata ReadFromPackage(string documentPath)
    {
        using var package = ZipFile.OpenRead(documentPath);

        var headerEntries = package.Entries
            .Where(entry => entry.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase) &&
                            entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var headers = new List<HeaderCandidate>();
        foreach (var headerEntry in headerEntries)
        {
            var relationshipsEntry = package.GetEntry($"word/_rels/{Path.GetFileName(headerEntry.FullName)}.rels");
            var relationships = relationshipsEntry is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : LoadRelationships(relationshipsEntry);

            headers.Add(ReadHeader(headerEntry, relationships));
        }

        if (headers.Count == 0)
        {
            return HeaderMetadata.Empty;
        }

        var bestHeader = headers
            .OrderByDescending(header => header.Score)
            .ThenByDescending(header => !string.IsNullOrWhiteSpace(header.Metadata.OdooUrl))
            .ThenByDescending(header => !string.IsNullOrWhiteSpace(header.Metadata.CounselorInitials))
            .First();

        var odooUrl = !string.IsNullOrWhiteSpace(bestHeader.Metadata.OdooUrl)
            ? bestHeader.Metadata.OdooUrl
            : headers.FirstOrDefault(header => !string.IsNullOrWhiteSpace(header.Metadata.OdooUrl))?.Metadata.OdooUrl ?? string.Empty;

        var counselorInitials = !string.IsNullOrWhiteSpace(bestHeader.Metadata.CounselorInitials)
            ? bestHeader.Metadata.CounselorInitials
            : headers.FirstOrDefault(header => !string.IsNullOrWhiteSpace(header.Metadata.CounselorInitials))?.Metadata.CounselorInitials ?? string.Empty;

        return new HeaderMetadata(odooUrl, counselorInitials);
    }

    private static Dictionary<string, string> LoadRelationships(ZipArchiveEntry relationshipsEntry)
    {
        using var stream = relationshipsEntry.Open();
        var document = new XmlDocument();
        document.Load(stream);

        var namespaceManager = new XmlNamespaceManager(document.NameTable);
        namespaceManager.AddNamespace("rel", "http://schemas.openxmlformats.org/package/2006/relationships");

        var relationships = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nodes = document.SelectNodes("/rel:Relationships/rel:Relationship", namespaceManager);
        if (nodes is null)
        {
            return relationships;
        }

        foreach (XmlNode node in nodes)
        {
            var id = node.Attributes?["Id"]?.Value;
            var target = node.Attributes?["Target"]?.Value;
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            relationships[id] = target;
        }

        return relationships;
    }

    private static HeaderCandidate ReadHeader(ZipArchiveEntry headerEntry, IReadOnlyDictionary<string, string> relationships)
    {
        using var stream = headerEntry.Open();
        var document = new XmlDocument();
        document.Load(stream);

        var namespaceManager = new XmlNamespaceManager(document.NameTable);
        namespaceManager.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
        namespaceManager.AddNamespace("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var textFragments = document.SelectNodes("//w:t", namespaceManager)?
            .Cast<XmlNode>()
            .Select(node => node.InnerText.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList() ?? new List<string>();

        var allText = Regex.Replace(string.Join(" ", textFragments), @"\s+", " ").Trim();

        var odooUrl = TryExtractOdooUrl(document, namespaceManager, relationships);

        var counselorInitials = TryExtractCounselorInitials(allText, textFragments);
        var score = ScoreHeader(allText, odooUrl, counselorInitials);

        return new HeaderCandidate(new HeaderMetadata(odooUrl ?? string.Empty, counselorInitials ?? string.Empty), score);
    }

    private static string? TryExtractOdooUrl(XmlDocument document, XmlNamespaceManager namespaceManager, IReadOnlyDictionary<string, string> relationships)
    {
        string? odooUrl = null;
        var hyperlinks = document.SelectNodes("//w:hyperlink", namespaceManager);
        if (hyperlinks is not null)
        {
            foreach (XmlNode hyperlink in hyperlinks)
            {
                var relationshipId = hyperlink.Attributes?["id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"]?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId) || !relationships.TryGetValue(relationshipId, out var target))
                {
                    continue;
                }

                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var anchor = hyperlink.Attributes?["anchor", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"]?.Value;
                    odooUrl ??= CombineTargetAndAnchor(target, anchor);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(odooUrl))
        {
            return odooUrl;
        }

        var fieldInstructionTexts = document.SelectNodes("//w:instrText", namespaceManager)?
            .Cast<XmlNode>()
            .Select(node => node.InnerText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList() ?? new List<string>();

        foreach (var instruction in fieldInstructionTexts)
        {
            var normalized = instruction.Trim();
            if (!normalized.Contains("HYPERLINK", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var urlMatch = Regex.Match(normalized, """https?://[^\s\"\']+""", RegexOptions.IgnoreCase);
            if (urlMatch.Success)
            {
                return urlMatch.Value;
            }
        }

        return null;
    }
    private static string? TryExtractCounselorInitials(string allText, IReadOnlyList<string> textFragments)
    {
        foreach (Match match in InlineCounselorRegex.Matches(allText))
        {
            if (!match.Success)
            {
                continue;
            }

            var candidate = NormalizeCounselorToken(match.Groups["code"].Value);
            if (candidate is not null)
            {
                return candidate;
            }
        }

        for (var index = 0; index < textFragments.Count; index++)
        {
            var fragment = textFragments[index];
            if (!fragment.Contains("Beratungsperson", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var combinedCandidate = TryCombineCounselorFragments(textFragments, index + 1);
            if (combinedCandidate is not null)
            {
                return combinedCandidate;
            }

            for (var offset = 0; offset < 5 && index + offset < textFragments.Count; offset++)
            {
                var candidate = NormalizeCounselorToken(textFragments[index + offset]);
                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string? NormalizeCounselorToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value
            .Replace("Beratungsperson", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(":", string.Empty)
            .Replace(" ", string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned) || string.Equals(cleaned, "XX", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!UpperTokenRegex.IsMatch(cleaned) || !CounselorInitialsRegex.IsMatch(cleaned))
        {
            return null;
        }

        return cleaned;
    }

    private static string? TryCombineCounselorFragments(IReadOnlyList<string> textFragments, int startIndex)
    {
        var parts = new List<string>(4);

        for (var index = startIndex; index < textFragments.Count && index < startIndex + 4; index++)
        {
            var normalized = NormalizeCounselorToken(textFragments[index]);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                break;
            }

            if (normalized.Length == 2)
            {
                return normalized;
            }

            parts.Add(normalized);
            if (parts.Count >= 2)
            {
                var combined = string.Concat(parts);
                return CounselorInitialsRegex.IsMatch(combined) ? combined : null;
            }
        }

        return null;
    }

    private static int ScoreHeader(string allText, string? odooUrl, string? counselorInitials)
    {
        var score = 0;

        if (allText.Contains("Verlaufsakte", StringComparison.OrdinalIgnoreCase))
        {
            score += 4;
        }

        if (allText.Contains("Abschlussbericht", StringComparison.OrdinalIgnoreCase))
        {
            score -= 2;
        }

        if (!string.IsNullOrWhiteSpace(odooUrl))
        {
            score += 5;
        }

        if (!string.IsNullOrWhiteSpace(counselorInitials))
        {
            score += 6;
        }

        if (PlaceholderHeaderRegex.IsMatch(allText))
        {
            score -= 8;
        }

        return score;
    }

    private static string CombineTargetAndAnchor(string target, string? anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor))
        {
            return target;
        }

        return target.Contains('#', StringComparison.Ordinal)
            ? $"{target}{anchor}"
            : $"{target}#{anchor}";
    }

    private sealed record CacheEntry(DateTime LastWriteTimeUtc, long Length, HeaderMetadata Metadata);
    private sealed record HeaderCandidate(HeaderMetadata Metadata, int Score);
}

public sealed record HeaderMetadata(string OdooUrl, string CounselorInitials)
{
    public static HeaderMetadata Empty { get; } = new(string.Empty, string.Empty);
}

public sealed class HeaderMetadataCacheDocument
{
    public int Version { get; set; }
    public List<HeaderMetadataCacheEntry> Entries { get; set; } = new();
}

public sealed class HeaderMetadataCacheEntry
{
    public string DocumentPath { get; set; } = string.Empty;
    public DateTime LastWriteTimeUtc { get; set; }
    public long Length { get; set; }
    public string OdooUrl { get; set; } = string.Empty;
    public string CounselorInitials { get; set; } = string.Empty;
}
