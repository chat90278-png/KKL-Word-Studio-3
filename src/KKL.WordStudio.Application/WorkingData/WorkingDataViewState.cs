namespace KKL.WordStudio.Application.WorkingData;

using KKL.WordStudio.Domain.DataSources;

/// <summary>
/// Preparation-only, worksheet-scoped view state for the Excel Workspace.
///
/// This type is strictly a <em>projection</em> over the live working data. It
/// never deletes rows or columns, never mutates <see cref="WorksheetWorkingData"/>,
/// never touches ColumnMappings/SourceField/Header semantics, and therefore
/// never changes ReportContentBuilder input, multi-source composition, Preview
/// or Word output. It exists only so the daily-prep grid can hide noise while
/// editing. It is deliberately not persisted into .kws this sprint.
///
/// The critical contract is <see cref="VisibleRowToWorkingRow"/>: editors must
/// translate a filtered display row back to its true underlying working-data
/// row index through this stable projection, never treat the display index as
/// product row identity.
/// </summary>
public sealed class WorkingDataViewState
{
    private string? _rowFilterText;
    private int? _rowFilterColumnIndex;
    private readonly HashSet<Guid> _hiddenColumnIds = new();

    /// <summary>Current contains-filter text, or null/empty when no filter is active.</summary>
    public string? RowFilterText => _rowFilterText;

    /// <summary>Working-data column index the filter applies to, or null when inactive.</summary>
    public int? RowFilterColumnIndex => _rowFilterColumnIndex;

    public bool HasRowFilter => !string.IsNullOrEmpty(_rowFilterText) && _rowFilterColumnIndex is not null;

    /// <summary>Sets a simple case-insensitive contains filter on one working-data column.</summary>
    public void SetRowFilter(int columnIndex, string? text)
    {
        _rowFilterColumnIndex = columnIndex;
        _rowFilterText = text;
    }

    public void ClearRowFilter()
    {
        _rowFilterText = null;
        _rowFilterColumnIndex = null;
    }

    public bool IsColumnHidden(WorkingDataColumn column) => _hiddenColumnIds.Contains(column.Id);

    /// <summary>Hides or shows a working-data column by its stable Id (view-only).</summary>
    public void SetColumnHidden(WorkingDataColumn column, bool hidden)
    {
        if (hidden) _hiddenColumnIds.Add(column.Id);
        else _hiddenColumnIds.Remove(column.Id);
    }

    public void RestoreAllColumns() => _hiddenColumnIds.Clear();

    /// <summary>
    /// Returns working-data row indexes that pass the current filter, in order.
    /// When no filter is active, every row index is returned. The result is a
    /// projection: element position is the display index, element value is the
    /// true underlying working-data row index.
    /// </summary>
    public IReadOnlyList<int> GetVisibleRowIndexes(WorksheetWorkingData data)
    {
        if (!HasRowFilter)
            return Enumerable.Range(0, data.Rows.Count).ToList();

        var columnIndex = _rowFilterColumnIndex!.Value;
        var text = _rowFilterText!;
        var visible = new List<int>();
        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            var values = data.Rows[rowIndex].Values;
            var value = columnIndex >= 0 && columnIndex < values.Count ? values[columnIndex] : null;
            if (value is not null && value.Contains(text, StringComparison.OrdinalIgnoreCase))
                visible.Add(rowIndex);
        }
        return visible;
    }

    /// <summary>
    /// Maps a filtered display row index back to the true working-data row
    /// index. Returns -1 when out of range. Editors MUST route edits through
    /// this so a visible filtered row updates the correct underlying row.
    /// </summary>
    public int VisibleRowToWorkingRow(WorksheetWorkingData data, int displayRowIndex)
    {
        var visible = GetVisibleRowIndexes(data);
        return displayRowIndex >= 0 && displayRowIndex < visible.Count ? visible[displayRowIndex] : -1;
    }

    /// <summary>Working-data column indexes currently visible, in order (row-number column handled by the view).</summary>
    public IReadOnlyList<int> GetVisibleColumnIndexes(WorksheetWorkingData data)
    {
        var visible = new List<int>();
        for (var columnIndex = 0; columnIndex < data.Columns.Count; columnIndex++)
            if (!_hiddenColumnIds.Contains(data.Columns[columnIndex].Id))
                visible.Add(columnIndex);
        return visible;
    }

    public void Clear()
    {
        ClearRowFilter();
        RestoreAllColumns();
    }
}
