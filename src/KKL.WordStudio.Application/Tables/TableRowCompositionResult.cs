namespace KKL.WordStudio.Application.Tables;

public sealed class TableRowCompositionResult
{
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    public required IReadOnlyList<TableCellSpan> CellSpans { get; init; }
    public required IReadOnlyList<TableRowGroup> RowGroups { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}
