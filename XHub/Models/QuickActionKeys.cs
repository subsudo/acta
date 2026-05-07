namespace XHub.Models;

public sealed class QuickActionDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public static class QuickActionKeys
{
    public const string Folder = "folder";
    public const string Document = "document";
    public const string Bu = "bu";
    public const string Bi = "bi";
    public const string Be = "be";
    public const string Lb = "lb";
    public const string EntryBu = "entry_bu";
    public const string EntryBi = "entry_bi";
    public const string EntryBe = "entry_be";
    public const string EntryLb = "entry_lb";

    public static IReadOnlyList<QuickActionDefinition> All { get; } = new[]
    {
        new QuickActionDefinition { Key = Folder, Label = "Ordner" },
        new QuickActionDefinition { Key = Document, Label = "Akte" },
        new QuickActionDefinition { Key = Bu, Label = "BU" },
        new QuickActionDefinition { Key = Bi, Label = "BI" },
        new QuickActionDefinition { Key = Be, Label = "BE" },
        new QuickActionDefinition { Key = Lb, Label = "LB" },
        new QuickActionDefinition { Key = EntryBu, Label = "Eintrag BU" },
        new QuickActionDefinition { Key = EntryBi, Label = "Eintrag BI" },
        new QuickActionDefinition { Key = EntryBe, Label = "Eintrag BE" },
        new QuickActionDefinition { Key = EntryLb, Label = "Eintrag LB" }
    };

    public static IReadOnlyList<string> CreateDefaults()
    {
        return new[] { Folder, Document };
    }
}
