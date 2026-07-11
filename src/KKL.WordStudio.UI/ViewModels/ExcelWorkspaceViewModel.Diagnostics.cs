namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.WorkingData;

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

        // Ensure the DataTable used by the grid is ready before searching and
        // before the view receives the cell-navigation request.
        await LoadPreviewAsync();

        if (string.IsNullOrWhiteSpace(keyValue))
        {
            StatusText = $"Uyarı kaynağı açıldı: {SelectedWorkbook.DisplayName} · {SelectedWorkbook.SelectedSheetName}";
            return true;
        }

        var worksheet = GetCurrentWorksheet();
        var matches = worksheet?.WorkingData is not null
            ? _workingDataService.Find(worksheet, keyValue)
            : FindInPreview(keyValue);
        if (matches.Count == 0)
            return false;

        var expectedColumnIndex = ResolveWorkingColumnIndex(source.KeyColumnIdentity);
        var match = expectedColumnIndex >= 0
            ? matches.FirstOrDefault(candidate => candidate.ColumnIndex == expectedColumnIndex)
            : default;
        if (match == default)
            match = matches[0];

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

    private int ResolveDiagnosticDisplayRow(
        Domain.DataSources.Worksheet? worksheet,
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
        Domain.DataSources.Worksheet? worksheet,
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
