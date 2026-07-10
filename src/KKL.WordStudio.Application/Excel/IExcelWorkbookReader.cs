namespace KKL.WordStudio.Application.Excel;

using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Application-facing contract for browsing an Excel file at design time:
/// listing its sheets and previewing raw cell content so the user can pick
/// a start row and let the system detect where the data ends. Distinct
/// from IDataProvider (which fetches actual typed rows for report
/// execution once a DataSource is fully configured) — this interface is
/// purely about the import/configuration workflow in the Excel Workspace.
/// Implemented in Infrastructure using the OpenXML SDK.
/// </summary>
public interface IExcelWorkbookReader
{
    /// <summary>Opens the workbook and returns its structural description (sheet names) without reading cell data.</summary>
    Task<Result<Workbook>> OpenWorkbookAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Reads up to <paramref name="maxPreviewRows"/> raw rows from a worksheet, for display while the user configures the data range.</summary>
    Task<Result<SheetPreview>> GetSheetPreviewAsync(
        string filePath, string worksheetName, int maxPreviewRows = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the complete configured dataset into a project-owned working-data
    /// snapshot. The source workbook is opened read-only and is never mutated.
    /// </summary>
    Task<Result<WorksheetWorkingData>> ReadWorkingDataAsync(
        string filePath, string worksheetName, DataRange range, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans the worksheet starting at <paramref name="dataStartRow"/> and returns a DataRange with
    /// DataEndRow set to the last row of a contiguous non-blank block (WasAutoDetected = true).
    /// The caller may subsequently overwrite DataEndRow manually, at which point it should also
    /// set WasAutoDetected = false to record that the value is no longer the system's guess.
    /// </summary>
    Task<Result<DataRange>> DetectDataRangeAsync(
        string filePath, string worksheetName, int dataStartRow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the full-sheet end row for a preview-detected data block while
    /// respecting its configured source-column bounds. The source workbook is
    /// opened read-only.
    /// </summary>
    async Task<Result<DataRange>> DetectDataRangeAsync(
        string filePath, string worksheetName, int dataStartRow, int startColumn, int endColumn, CancellationToken cancellationToken = default)
    {
        var result = await DetectDataRangeAsync(filePath, worksheetName, dataStartRow, cancellationToken);
        if (result.IsFailure) return result;
        result.Value.StartColumn = startColumn;
        result.Value.EndColumn = endColumn;
        return result;
    }
}

