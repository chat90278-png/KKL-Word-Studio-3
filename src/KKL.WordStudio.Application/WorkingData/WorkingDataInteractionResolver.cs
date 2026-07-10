namespace KKL.WordStudio.Application.WorkingData;

using KKL.WordStudio.Domain.DataSources;

/// <summary>
/// Resolves runtime grid projection identity back to project-owned working-data
/// identity. Filtered row positions and visible DataGrid column positions are
/// never accepted as product indexes.
/// </summary>
public static class WorkingDataInteractionResolver
{
    public static int ResolveRowIndex(WorksheetWorkingData data, WorkingDataViewState viewState, int displayRowIndex) =>
        viewState.VisibleRowToWorkingRow(data, displayRowIndex);

    public static IReadOnlyList<int> ResolveRowIndexes(
        WorksheetWorkingData data,
        WorkingDataViewState viewState,
        IEnumerable<int> displayRowIndexes) =>
        displayRowIndexes
            .Distinct()
            .Select(index => ResolveRowIndex(data, viewState, index))
            .Where(index => index >= 0)
            .Distinct()
            .ToList();

    public static int ResolveColumnIndex(WorksheetWorkingData data, string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity)) return -1;
        for (var index = 0; index < data.Columns.Count; index++)
        {
            var column = data.Columns[index];
            if (string.Equals(column.SourceField, identity, StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.OriginalSourceColumn, identity, StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.Id.ToString("D"), identity, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return -1;
    }

    public static IReadOnlyList<int> ResolveColumnIndexes(WorksheetWorkingData data, IEnumerable<string> identities) =>
        identities
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(identity => ResolveColumnIndex(data, identity))
            .Where(index => index >= 0)
            .Distinct()
            .ToList();
}
