namespace KKL.WordStudio.Application.Tables;

/// <summary>
/// Complete semantic table row-group input for layout pagination.
/// </summary>
public sealed class TableRowGroup
{
    public required int StartRowIndex { get; init; }
    public required int RowCount { get; init; }
    public required bool KeepTogetherWhenPossible { get; init; }
}
