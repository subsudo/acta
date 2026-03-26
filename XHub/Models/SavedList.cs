namespace XHub.Models;

public class SavedList
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Meine Liste";
    public int SortOrder { get; set; }
    public List<SavedListItem> Items { get; set; } = new();
    public List<DetailModuleConfig> Modules { get; set; } = DetailModuleKeys.CreateDefaults().Select(x => x.Clone()).ToList();
}
