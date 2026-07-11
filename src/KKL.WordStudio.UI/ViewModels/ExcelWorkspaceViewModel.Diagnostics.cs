namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;

public sealed partial class ExcelWorkspaceViewModel
{
    public event Action<ExcelGridNavigationRequest>? DiagnosticGridNavigationRequested;

    public async Task<bool> NavigateToDiagnosticSourceAsync(
        PreviewDiagnosticSource source,
        string? keyValue)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.SourcePath))
        {
            StatusText = $"'{source.DataSourceName}' kaynağının dosya yolu bulunamadı.";
            return false;
        }

        await NavigateToWorksheetAsync(source.SourcePath, source.WorksheetName);

        if (SelectedWorkbook is null
            || !string.Equals(SelectedWorkbook.FilePath, source.SourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // NavigateToWorksheetAsync may switch the selected workbook through a
        // generated property-change hook. Await one explicit load so the grid
        // projection is definitely ready before cell resolution.
        await LoadPreviewAsync();

        if (string.IsNullOrWhiteSpace(keyValue))
        {
            StatusText = $"Uyarı kaynağı açıldı: {SelectedWorkbook.DisplayName} · {SelectedWorkbook.SelectedSheetName}";
            return true;
        }

        var worksheet = GetCurrentWorksheet();
        var matches = FindDiagnosticMatches(worksheet, keyValue);
        if (matches.Count == 0)
            return false;

        var expectedColumnIndex = ResolveWorkingColumnIndex(source.KeyColumnIdentity);
        var preferredMatches = expectedColumnIndex >= 0
            ? matches.Where(candidate => candidate.ColumnIndex == expectedColumnIndex).ToList()
            : [];
        var match = preferredMatches.Count > 0 ? preferredMatches[0] : matches[0];

        var displayRowIndex = ResolveDiagnosticDisplayRow(worksheet, match.RowIndex);
        if (displayRowIndex < 0)
            return false;

        var columnIdentity = ResolveDiagnosticColumnIdentity(worksheet, match.ColumnIndex);
        DiagnosticGridNavigationRequested?.Invoke(new ExcelGridNavigationRequest(
            displayRowIndex,
            match.ColumnIndex,
            columnIdentity));

        FindText = keyValue;
        FindStatusText = $"Uyarı anahtarı bulundu: {keyValue}";
        StatusText = $"Uyarı kaynağına gidildi · {SelectedWorkbook.DisplayName} · {SelectedWorkbook.SelectedSheetName}";
        return true;
    }

    private IReadOnlyList<WorkingDataCell> FindDiagnosticMatches(
        Worksheet? worksheet,
        string keyValue)
    {
        var allMatches = worksheet?.WorkingData is not null
            ? _workingDataService.Find(worksheet, keyValue)
            : FindInPreview(keyValue);

        // Existing Find intentionally uses contains semantics. Diagnostic
        // navigation first prefers exact cell text so key 55 does not jump to
        // 9555, then falls back to the existing search behavior.
        var exactMatches = allMatches
            .Where(match => string.Equals(
                ReadDiagnosticCellText(worksheet, match),
                keyValue,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        return exactMatches.Count > 0 ? exactMatches : allMatches;
    }

    private string? ReadDiagnosticCellText(Worksheet? worksheet, WorkingDataCell cell)
    {
        if (worksheet?.WorkingData is { } workingData
            && cell.RowIndex >= 0
            && cell.RowIndex < workingData.Rows.Count
            && cell.ColumnIndex >= 0
            && cell.ColumnIndex < workingData.Columns.Count)
        {
            return workingData.Rows[cell.RowIndex].Values[cell.ColumnIndex];
        }

        if (_currentPreview is not null
            && cell.RowIndex >= 0
            && cell.RowIndex < _currentPreview.Rows.Count
            && cell.ColumnIndex >= 0
            && cell.ColumnIndex < _currentPreview.Rows[cell.RowIndex].Count)
        {
            return _currentPreview.Rows[cell.RowIndex][cell.ColumnIndex];
        }

        return null;
    }

    private int ResolveDiagnosticDisplayRow(
        Worksheet? worksheet,
        int workingRowIndex)
    {
        if (worksheet?.WorkingData is not { } workingData)
            return workingRowIndex;

        var view = ViewStateFor(worksheet);
        if (!view.HasRowFilter)
            return workingRowIndex;

        var visibleRows = view.GetVisibleRowIndexes(workingData).ToList();
        var displayIndex = visibleRows.IndexOf(workingRowIndex);
        if (displayIndex >= 0)
            return displayIndex;

        // A diagnostic target must not remain hidden behind a preparation-only
        // filter. Clear the view filter without changing report/Word semantics.
        view.ClearRowFilter();
        RowFilterText = string.Empty;
        RefreshWorkingDataView(worksheet);
        OnPropertyChanged(nameof(RowFilterStatusText));
        return workingRowIndex;
    }

    private string? ResolveDiagnosticColumnIdentity(
        Worksheet? worksheet,
        int columnIndex)
    {
        if (worksheet?.WorkingData is { } workingData
            && columnIndex >= 0
            && columnIndex < workingData.Columns.Count)
        {
            return workingData.Columns[columnIndex].SourceField;
        }

        return columnIndex >= 0 && columnIndex < PreviewTable.Columns.Count
            ? PreviewTable.Columns[columnIndex].ColumnName
            : null;
    }
}

public readonly record struct ExcelGridNavigationRequest(
    int DisplayRowIndex,
    int ColumnIndex,
    string? ColumnIdentity);
