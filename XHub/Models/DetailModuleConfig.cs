namespace XHub.Models;

public class DetailModuleConfig
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Order { get; set; }

    public DetailModuleConfig Clone()
    {
        return new DetailModuleConfig
        {
            Key = Key,
            Title = Title,
            IsEnabled = IsEnabled,
            Order = Order
        };
    }
}
