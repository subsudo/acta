using System.IO;
using XHub.Models;

namespace XHub.Services;

public class ExportService
{
    public void Export(string targetPath, IEnumerable<SavedList> lists)
    {
        var package = new ExportPackage
        {
            CreatedAtUtc = DateTime.UtcNow,
            Lists = lists.Select(CloneList).ToList()
        };

        JsonStorage.SaveAtomic(targetPath, null, package);
    }

    public List<SavedList> Import(string sourcePath)
    {
        var package = JsonStorage.LoadRequired<ExportPackage>(sourcePath);
        var lists = (package.Lists ?? new List<SavedList>())
            .Select(CloneList)
            .ToList();
        if (lists.Count == 0)
        {
            throw new InvalidDataException("Der XHub-Export enthaelt keine Listen.");
        }

        return lists;
    }

    private static SavedList CloneList(SavedList source)
    {
        return new SavedList
        {
            Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id,
            Name = source.Name,
            SortOrder = source.SortOrder,
            Items = (source.Items ?? new List<SavedListItem>()).Select(item => new SavedListItem
            {
                ParticipantKey = item.ParticipantKey,
                SortOrder = item.SortOrder
            }).ToList(),
            Modules = (source.Modules ?? new List<DetailModuleConfig>()).Select(module => module.Clone()).ToList()
        };
    }
}
