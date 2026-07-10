namespace KKL.WordStudio.Application.WorkingData;

using KKL.WordStudio.Domain.DataSources;

/// <summary>
/// Worksheet-scoped runtime undo/redo history over working-data snapshots.
///
/// One instance belongs to exactly one worksheet; histories are never shared
/// across worksheets (see <see cref="WorkingDataHistoryRegistry"/>), so
/// switching sheets can never mix undo stacks. The history is bounded to
/// <see cref="DefaultCapacity"/> steps to keep runtime memory predictable for
/// the daily-preparation dataset model.
///
/// History is runtime-only and is never persisted into .kws.
/// </summary>
public sealed class WorkingDataHistory
{
    public const int DefaultCapacity = 50;

    private readonly int _capacity;
    private readonly LinkedList<WorkingDataSnapshot> _undo = new();
    private readonly Stack<WorkingDataSnapshot> _redo = new();

    public WorkingDataHistory(int capacity = DefaultCapacity) =>
        _capacity = Math.Max(1, capacity);

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Records a completed mutation by pushing the pre-mutation snapshot onto
    /// the undo stack. Any pending redo branch is discarded, because a new
    /// mutation after an undo invalidates the previously undone future.
    /// Call this exactly once per successful mutation (one paste, one multi-cell
    /// clear, one multi-row delete, one Replace All each count as one step).
    /// </summary>
    public void Record(WorkingDataSnapshot before)
    {
        _undo.AddLast(before);
        if (_undo.Count > _capacity) _undo.RemoveFirst();
        _redo.Clear();
    }

    /// <summary>
    /// Undoes the most recent recorded mutation: captures the current state as
    /// a redo point, then restores the previous snapshot in place. Returns false
    /// when there is nothing to undo.
    /// </summary>
    public bool Undo(WorksheetWorkingData current)
    {
        if (_undo.Count == 0) return false;
        var previous = _undo.Last!.Value;
        _undo.RemoveLast();
        _redo.Push(WorkingDataSnapshot.Capture(current));
        previous.RestoreInto(current);
        return true;
    }

    /// <summary>
    /// Reapplies the most recently undone mutation. Returns false when there is
    /// nothing to redo.
    /// </summary>
    public bool Redo(WorksheetWorkingData current)
    {
        if (_redo.Count == 0) return false;
        var next = _redo.Pop();
        _undo.AddLast(WorkingDataSnapshot.Capture(current));
        if (_undo.Count > _capacity) _undo.RemoveFirst();
        next.RestoreInto(current);
        return true;
    }

    /// <summary>Clears both stacks — used by reset-to-source and stale-history disposal.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
