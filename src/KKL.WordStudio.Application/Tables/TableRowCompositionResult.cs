namespace KKL.WordStudio.Application.Tables;

public sealed class TableRowCompositionResult
{
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    public required IReadOnlyList<TableCellSpan> CellSpans { get; init; }
    public required IReadOnlyList<TableRowGroup> RowGroups { get; init; }

    /// <summary>
    /// Technical composition messages retained as the frozen Sprint 15 contract.
    /// The report-content boundary projects these messages into structured
    /// diagnostics without expanding this public result shape.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
