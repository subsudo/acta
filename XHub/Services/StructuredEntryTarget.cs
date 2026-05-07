namespace XHub.Services;

public sealed class StructuredEntryTarget
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string TableBookmarkName { get; init; }
    public int FirstDataRowIndex { get; init; } = 2;
    public int ExpectedColumnCount { get; init; } = 4;
    public int ValidClipboardFocusColumn { get; init; } = 1;
    public int FallbackFocusColumn { get; init; } = 3;
    public bool BringToForeground { get; init; } = true;

    public static StructuredEntryTarget Bu { get; } = new()
    {
        Key = "BU",
        Label = "Eintrag BU",
        TableBookmarkName = "BU_BILDUNG_TABELLE"
    };

    public static StructuredEntryTarget Bi { get; } = new()
    {
        Key = "BI",
        Label = "Eintrag BI",
        TableBookmarkName = "BI_BERUFSINTEGRATION_TABELLE"
    };

    public static StructuredEntryTarget Be { get; } = new()
    {
        Key = "BE",
        Label = "Eintrag BE",
        TableBookmarkName = "BE_BERATUNG_TABELLE"
    };

    public static StructuredEntryTarget Lb { get; } = new()
    {
        Key = "LB",
        Label = "Eintrag LB",
        TableBookmarkName = "LB_LEHRBEGLEITUNG_TABELLE"
    };
}
