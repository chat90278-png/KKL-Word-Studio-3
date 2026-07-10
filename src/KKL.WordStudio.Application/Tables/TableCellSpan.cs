namespace KKL.WordStudio.Application.Tables;

/// <summary>
/// Vertical table-cell span. RowIndex is relative to the owning row collection;
/// ColumnIndex is zero-based. Emitted spans must have RowSpan of at least two.
/// </summary>
public sealed class TableCellSpan
{
    public required int RowIndex { get; init; }
    public required int ColumnIndex { get; init; }
    public required int RowSpan { get; init; }
}
