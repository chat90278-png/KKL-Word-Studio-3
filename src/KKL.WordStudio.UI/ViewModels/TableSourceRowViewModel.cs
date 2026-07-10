namespace KKL.WordStudio.UI.ViewModels;

public sealed class TableSourceRowViewModel
{
    public required int Index { get; init; }
    public required string DataSourceName { get; init; }
    public required string WorksheetName { get; init; }
    public required string RangeText { get; init; }
    public required string StatusText { get; init; }
    public bool IsUsable { get; init; }
    public bool IsLegacyBinding { get; init; }

    public string SourceDisplay => DataSourceName;
    public string WorksheetRangeDisplay => $"{WorksheetName} · {RangeText}";
}
