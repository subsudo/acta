using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace XHub.Services;

public sealed class ParticipantNotesService
{
    private readonly string _notesDirectoryPath;

    public ParticipantNotesService(string notesDirectoryPath)
    {
        _notesDirectoryPath = notesDirectoryPath;
    }

    public void LoadInto(string participantKey, FlowDocument document)
    {
        document.Blocks.Clear();
        var path = GetNotePath(participantKey);
        if (!File.Exists(path))
        {
            document.Blocks.Add(CreateEmptyParagraph());
            return;
        }

        try
        {
            var range = new TextRange(document.ContentStart, document.ContentEnd);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            range.Load(stream, DataFormats.XamlPackage);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Teilnehmernotiz konnte nicht geladen werden '{path}': {ex.Message}");
            document.Blocks.Clear();
            document.Blocks.Add(CreateEmptyParagraph());
        }
    }

    public void SaveOrDelete(string participantKey, FlowDocument document)
    {
        if (IsDocumentEmpty(document))
        {
            Delete(participantKey);
            return;
        }

        Directory.CreateDirectory(_notesDirectoryPath);
        var path = GetNotePath(participantKey);
        var tempPath = Path.Combine(_notesDirectoryPath, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var range = new TextRange(document.ContentStart, document.ContentEnd);
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                range.Save(stream, DataFormats.XamlPackage);
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            AppLogger.Warn($"Teilnehmernotiz konnte nicht gespeichert werden '{path}': {ex.Message}");
        }
    }

    private void Delete(string participantKey)
    {
        TryDelete(GetNotePath(participantKey));
    }

    private string GetNotePath(string participantKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(participantKey));
        var fileName = Convert.ToHexString(hash).ToLowerInvariant() + ".xamlpackage";
        return Path.Combine(_notesDirectoryPath, fileName);
    }

    private static bool IsDocumentEmpty(FlowDocument document)
    {
        var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
        return string.IsNullOrWhiteSpace(text);
    }

    private static Paragraph CreateEmptyParagraph() => new(new Run(string.Empty));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Teilnehmernotiz konnte nicht gelöscht werden '{path}': {ex.Message}");
        }
    }
}
