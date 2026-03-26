namespace XHub.Models;

public static class DetailModuleKeys
{
    public const string Overview = "overview";
    public const string Image = "image";
    public const string Initials = "initials";
    public const string Actions = "actions";

    public static IReadOnlyList<DetailModuleConfig> CreateDefaults()
    {
        return new[]
        {
            new DetailModuleConfig { Key = Overview, Title = "Übersicht", IsEnabled = true, Order = 0 },
            new DetailModuleConfig { Key = Image, Title = "Bild", IsEnabled = true, Order = 1 },
            new DetailModuleConfig { Key = Initials, Title = "Kürzel", IsEnabled = true, Order = 2 },
            new DetailModuleConfig { Key = Actions, Title = "Aktenaktionen", IsEnabled = true, Order = 3 }
        };
    }
}
