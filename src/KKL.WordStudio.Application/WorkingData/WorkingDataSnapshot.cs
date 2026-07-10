namespace KKL.WordStudio.Application.WorkingData;

using KKL.WordStudio.Domain.DataSources;

/// <summary>
/// An immutable, deep copy of a worksheet's <see cref="WorksheetWorkingData"/>
/// used as a bounded before/after undo/redo record.
///
/// Sprint 11 deliberately uses whole-snapshot capture rather than a per-op
/// reversible command log: the working-data model is a single small ordered
/// table (no formulas, no cross-sheet references), so snapshots are the
/// smallest maintainable design that provably restores prior state for every
/// mutation kind (edit / clear / paste / insert-row / delete-rows /
/// insert-column / delete-columns / replace) without bespoke inverse logic.
///
/// Snapshots are runtime-only. They are never written into .kws — only the
/// live <see cref="WorksheetWorkingData"/> is persisted, exactly as before.
/// </summary>
public sealed class WorkingDataSnapshot
{
    private readonly List<ColumnState> _columns;
    private readonly List<RowState> _rows;

    private WorkingDataSnapshot(List<ColumnState> columns, List<RowState> rows)
    {
        _columns = columns;
        _rows = rows;
    }

    /// <summary>Deep-copies the current working data into an immutable snapshot.</summary>
    public static WorkingDataSnapshot Capture(WorksheetWorkingData data)
    {
        var columns = data.Columns
            .Select(column => new ColumnState(column.Id, column.SourceField, column.Header, column.OriginalSourceColumn))
            .ToList();
        var rows = data.Rows
            .Select(row => new RowState(row.OriginalRowNumber, row.Values.ToList()))
            .ToList();
        return new WorkingDataSnapshot(columns, rows);
    }

    /// <summary>
    /// Restores this snapshot into the target working data in place, preserving
    /// the same <see cref="WorksheetWorkingData"/> instance and its identity so
    /// existing references (composition, preview) stay valid. Column
    /// <see cref="WorkingDataColumn.Id"/> is preserved across the round trip, so
    /// stable binding identity survives undo/redo.
    /// </summary>
    public void RestoreInto(WorksheetWorkingData data)
    {
        data.Columns.Clear();
        foreach (var column in _columns)
        {
            data.Columns.Add(new WorkingDataColumn
            {
                Id = column.Id,
                SourceField = column.SourceField,
                Header = column.Header,
                OriginalSourceColumn = column.OriginalSourceColumn
            });
        }

        data.Rows.Clear();
        foreach (var row in _rows)
        {
            var restored = new WorkingDataRow { OriginalRowNumber = row.OriginalRowNumber };
            restored.Values.AddRange(row.Values);
            data.Rows.Add(restored);
        }
    }

    private sealed record ColumnState(Guid Id, string SourceField, string Header, string? OriginalSourceColumn);

    private sealed record RowState(int? OriginalRowNumber, List<string?> Values);
}
