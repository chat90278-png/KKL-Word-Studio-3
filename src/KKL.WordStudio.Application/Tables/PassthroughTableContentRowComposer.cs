namespace KKL.WordStudio.Application.Tables;

using KKL.WordStudio.Domain.Elements;

/// <summary>
/// Bootstrap composer. It intentionally provides no grouping heuristics and
/// preserves the normalized row stream exactly as received.
/// </summary>
public sealed class PassthroughTableContentRowComposer : ITableContentRowComposer
{
    public TableRowCompositionResult Compose(
        TableElement table,
        IReadOnlyList<IReadOnlyList<string>> normalizedRows)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(normalizedRows);

        return new TableRowCompositionResult
        {
            Rows = normalizedRows,
            CellSpans = Array.Empty<TableCellSpan>(),
            RowGroups = Array.Empty<TableRowGroup>(),
            Warnings = Array.Empty<string>()
        };
    }
}
