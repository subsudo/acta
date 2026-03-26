using XHub.Models;

namespace XHub.Services;

public class ListRepository
{
    private readonly string _listsPath;
    private readonly string _backupPath;

    public ListRepository(string listsPath, string backupPath)
    {
        _listsPath = listsPath;
        _backupPath = backupPath;
    }

    public List<SavedList> Load()
    {
        var lists = JsonStorage.Load(_listsPath, _backupPath, CreateDefaultLists);
        Normalize(lists);
        return lists;
    }

    public void Save(IEnumerable<SavedList> lists)
    {
        var normalized = lists
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Normalize(normalized);
        JsonStorage.SaveAtomic(_listsPath, _backupPath, normalized);
    }

    public static List<DetailModuleConfig> NormalizeModules(IEnumerable<DetailModuleConfig>? modules)
    {
        var incoming = new Dictionary<string, DetailModuleConfig>(StringComparer.OrdinalIgnoreCase);
        if (modules is not null)
        {
            foreach (var module in modules)
            {
                if (module is null || string.IsNullOrWhiteSpace(module.Key))
                {
                    continue;
                }

                incoming[module.Key] = module;
            }
        }

        return DetailModuleKeys.CreateDefaults()
            .Select(defaultModule =>
            {
                if (incoming.TryGetValue(defaultModule.Key, out var existing))
                {
                    return new DetailModuleConfig
                    {
                        Key = defaultModule.Key,
                        Title = defaultModule.Title,
                        IsEnabled = existing.IsEnabled,
                        Order = existing.Order
                    };
                }

                return defaultModule.Clone();
            })
            .OrderBy(x => x.Order)
            .ToList();
    }

    private static List<SavedList> CreateDefaultLists()
    {
        return new List<SavedList>();
    }

    private static void Normalize(List<SavedList> lists)
    {
        for (var listIndex = 0; listIndex < lists.Count; listIndex++)
        {
            var list = lists[listIndex];
            if (string.IsNullOrWhiteSpace(list.Id))
            {
                list.Id = Guid.NewGuid().ToString("N");
            }

            list.Name = string.IsNullOrWhiteSpace(list.Name) ? $"Liste {listIndex + 1}" : list.Name.Trim();
            list.SortOrder = listIndex;
            list.Items ??= new List<SavedListItem>();
            list.Modules = NormalizeModules(list.Modules);

            for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
            {
                list.Items[itemIndex].SortOrder = itemIndex;
            }
        }
    }
}
