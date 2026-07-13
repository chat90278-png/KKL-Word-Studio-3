namespace KKL.WordStudio.Application.Transfer;

using System.Collections.Concurrent;
using KKL.WordStudio.Shared.Spreadsheet;

/// <summary>
/// Runtime-only selection of source columns that are allowed to become report
/// columns. The key is the loaded workbook path + worksheet name. Nothing from
/// this store is persisted into Project/Domain or written back to Excel.
/// </summary>
public interface IColumnTransferSelectionSession
{
    IReadOnlyList<string>? GetSelection(string workbookFilePath, string worksheetName);
    void SetSelection(string workbookFilePath, string worksheetName, IEnumerable<string> sourceColumns);
    void ClearSelection(string workbookFilePath, string worksheetName);
}

public sealed class ColumnTransferSelectionSession : IColumnTransferSelectionSession
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> selections =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Shared desktop-session instance. The WPF authoring surface and the
    /// transfer decorator use the same in-memory state without adding it to the
    /// long-lived project model.
    /// </summary>
    public static ColumnTransferSelectionSession Shared { get; } = new();

    public IReadOnlyList<string>? GetSelection(string workbookFilePath, string worksheetName) =>
        selections.TryGetValue(BuildKey(workbookFilePath, worksheetName), out var value)
            ? value
            : null;

    public void SetSelection(
        string workbookFilePath,
        string worksheetName,
        IEnumerable<string> sourceColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worksheetName);
        ArgumentNullException.ThrowIfNull(sourceColumns);

        var normalized = sourceColumns
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Select(NormalizeColumnIdentity)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ColumnOrder)
            .ThenBy(column => column, StringComparer.OrdinalIgnoreCase)
            .ToList();

        selections[BuildKey(workbookFilePath, worksheetName)] = normalized;
    }

    public void ClearSelection(string workbookFilePath, string worksheetName) =>
        selections.TryRemove(BuildKey(workbookFilePath, worksheetName), out _);

    private static string BuildKey(string workbookFilePath, string worksheetName) =>
        $"{Path.GetFullPath(workbookFilePath).Trim()}\u001f{worksheetName.Trim()}";

    private static string NormalizeColumnIdentity(string value)
    {
        var trimmed = value.Trim();
        try
        {
            return ColumnLetterConverter.ToLetters(ColumnLetterConverter.ToIndex(trimmed));
        }
        catch (ArgumentException)
        {
            // WorkingData columns may use stable non-letter SourceField values.
            return trimmed;
        }
    }

    private static int ColumnOrder(string value)
    {
        try
        {
            return ColumnLetterConverter.ToIndex(value);
        }
        catch (ArgumentException)
        {
            return int.MaxValue;
        }
    }
}
