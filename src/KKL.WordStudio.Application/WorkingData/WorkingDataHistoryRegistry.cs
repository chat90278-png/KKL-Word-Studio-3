namespace KKL.WordStudio.Application.WorkingData;

using System.Runtime.CompilerServices;
using KKL.WordStudio.Domain.DataSources;

public interface IWorkingDataHistoryRegistry
{
    /// <summary>Returns the history for a worksheet, creating an empty one on first access.</summary>
    WorkingDataHistory For(Worksheet worksheet);

    /// <summary>Removes a single worksheet's history (reset-to-source).</summary>
    void Forget(Worksheet worksheet);

    /// <summary>Drops all runtime histories — used on project open/new so stale history never resurrects.</summary>
    void Clear();
}

/// <summary>
/// Keys undo/redo histories by worksheet <em>instance identity</em> via a
/// <see cref="ConditionalWeakTable{TKey,TValue}"/>. Using reference identity
/// (not name/path) guarantees each configured worksheet gets its own isolated
/// stack and that switching sheets can never mix histories. Because the table
/// holds weak keys, worksheets dropped when a project closes do not keep their
/// histories alive; <see cref="Clear"/> additionally forces a hard reset on
/// project open/new so no stale runtime history is ever observable.
/// </summary>
public sealed class WorkingDataHistoryRegistry : IWorkingDataHistoryRegistry
{
    private readonly int _capacity;
    private ConditionalWeakTable<Worksheet, WorkingDataHistory> _histories = new();

    public WorkingDataHistoryRegistry(int capacity = WorkingDataHistory.DefaultCapacity) =>
        _capacity = capacity;

    public WorkingDataHistory For(Worksheet worksheet) =>
        _histories.GetValue(worksheet, _ => new WorkingDataHistory(_capacity));

    public void Forget(Worksheet worksheet)
    {
        if (_histories.TryGetValue(worksheet, out var history))
        {
            history.Clear();
            _histories.Remove(worksheet);
        }
    }

    // Replacing the table (rather than clearing keys one by one) drops every
    // reference in a single step, which is the cheapest correct way to ensure
    // a freshly opened/created project starts with no runtime history.
    public void Clear() => _histories = new ConditionalWeakTable<Worksheet, WorkingDataHistory>();
}
