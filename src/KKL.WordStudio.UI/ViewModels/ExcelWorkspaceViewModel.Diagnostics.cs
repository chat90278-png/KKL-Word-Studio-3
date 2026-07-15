namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;

public sealed partial class ExcelWorkspaceViewModel
{
    private static readonly IExcelSemanticFieldMatcher DiagnosticFieldMatcher = new ExcelSemanticFieldMatcher();

    public event Action<ExcelGridNavigationRequest>? DiagnosticGridNavigationRequested;

    public Task<bool> NavigateToDiagnosticSourceAsync(
        PreviewDiagnosticSource source,
        string? keyValue) =>
        NavigateToDiagnosticSourceAsync(
            source,
            string.IsNullOrWhiteSpace(keyValue) ? Array.Empty<string>() : new[] { keyValue! },
            affectedColumn: null);

    public async Task<bool> NavigateToDiagnosticSourceAsync(
        PreviewDiagnosticSource source,
        IReadOnlyList<string> keyValues,
        string? affectedColumn)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(keyValues);

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

        var distinctKeys = keyValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinctKeys.Count == 0)
        {
            StatusText = $"Uyarı kaynağı açıldı: {SelectedWorkbook.DisplayName} · {SelectedWorkbook.SelectedSheetName}";
            return true;
        }

        var worksheet = GetCurrentWorksheet();
        var expectedKeyColumnIndex = ResolveDiagnosticColumnIndex(worksheet, source.KeyColumnIdentity);
        var affectedColumnIndex = ResolveDiagnosticColumnIndex(worksheet, affectedColumn);

        foreach (var keyValue in distinctKeys)
        {
            var matches = FindDiagnosticMatches(worksheet, keyValue);
            if (matches.Count == 0)
                continue;

            WorkingDataCell keyMatch;
            if (expectedKeyColumnIndex >= 0)
            {
                var preferredMatches = matches
                    .Where(candidate => candidate.ColumnIndex == expectedKeyColumnIndex)
                    .ToList();
                if (preferredMatches.Count == 0)
                    continue;
                keyMatch = preferredMatches[0];
            }
            else
            {
                keyMatch = matches[0];
            }

            var targetColumnIndex = affectedColumnIndex >= 0
                ? affectedColumnIndex
                : keyMatch.ColumnIndex;

            EnsureDiagnosticColumnVisible(worksheet, targetColumnIndex);
            var displayRowIndex = ResolveDiagnosticDisplayRow(worksheet, keyMatch.RowIndex);
            if (displayRowIndex < 0)
                continue;

            var columnIdentity = ResolveDiagnosticColumnIdentity(worksheet, targetColumnIndex);
            DiagnosticGridNavigationRequested?.Invoke(new ExcelGridNavigationRequest(
                displayRowIndex,
                targetColumnIndex,
                columnIdentity));

            FindText = keyValue;
            var targetLabel = string.IsNullOrWhiteSpace(affectedColumn) ? "uyarı" : affectedColumn.Trim();
            FindStatusText = $"{targetLabel} hücresi bulundu · Anahtar: {keyValue}";
            StatusText = $"Sorunlu hücreye gidildi · {SelectedWorkbook.DisplayName} · {SelectedWorkbook.SelectedSheetName}";
            return true;
        }

        StatusText = "Uyarının kayıt anahtarı yapılandırılmış anahtar sütununda tam eşleşmeyle bulunamadı.";
        return false;
    }

    private IReadOnlyList<WorkingDataCell> FindDiagnosticMatches(
        Worksheet? worksheet,
        string keyValue)
    {
        var allMatches = worksheet?.WorkingData is not null
            ? _workingDataService.Find(worksheet, keyValue)
            : FindInPreview(keyValue);

        // Existing user Find intentionally uses contains semantics. Diagnostic
        // navigation must never fall back to that behavior: key 55 must not jump
        // to 9555 or another record merely because the text contains the key.
        var normalizedKey = keyValue.Trim();
        return allMatches
            .Where(match => string.Equals(
                ReadDiagnosticCellText(worksheet, match)?.Trim(),
                normalizedKey,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
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

    private int ResolveDiagnosticColumnIndex(Worksheet? worksheet, string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            return -1;

        if (worksheet?.WorkingData is { } workingData)
        {
            for (var index = 0; index < workingData.Columns.Count; index++)
            {
                var column = workingData.Columns[index];
                if (DiagnosticColumnMatches(identity, column.SourceField)
                    || DiagnosticColumnMatches(identity, column.OriginalSourceColumn)
                    || DiagnosticColumnMatches(identity, column.Header)
                    || string.Equals(column.Id.ToString("D"), identity, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
        }

        var existingIndex = ResolveWorkingColumnIndex(identity);
        if (existingIndex >= 0)
            return existingIndex;

        if (_currentPreview is null || HeaderRowNumber is not { } headerRowNumber)
            return -1;

        var headerPreviewIndex = IndexOfValue(_currentPreview.RowNumbers, headerRowNumber);
        if (headerPreviewIndex < 0 || headerPreviewIndex >= _currentPreview.Rows.Count)
            return -1;

        var headers = _currentPreview.Rows[headerPreviewIndex];
        for (var index = 0; index < headers.Count; index++)
        {
            if (DiagnosticColumnMatches(identity, headers[index]))
                return index;
        }

        return -1;
    }

    private void EnsureDiagnosticColumnVisible(Worksheet? worksheet, int columnIndex)
    {
        if (worksheet?.WorkingData is not { } workingData
            || columnIndex < 0
            || columnIndex >= workingData.Columns.Count)
        {
            return;
        }

        var view = ViewStateFor(worksheet);
        if (!view.IsColumnHidden(workingData.Columns[columnIndex]))
            return;

        view.SetColumnHidden(workingData.Columns[columnIndex], false);
        RefreshWorkingDataView(worksheet);
    }

    private static bool DiagnosticColumnMatches(string requested, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (string.Equals(requested.Trim(), candidate.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        var requestedRole = DiagnosticFieldMatcher.Match(requested);
        return requestedRole != ExcelSemanticFieldRole.Unknown
            && requestedRole == DiagnosticFieldMatcher.Match(candidate);
    }

    private static int IndexOfValue<T>(IReadOnlyList<T> items, T value)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < items.Count; index++)
        {
            if (comparer.Equals(items[index], value))
                return index;
        }

        return -1;
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

        // PreviewTable contains the hidden row-number metadata column (#) at
        // index zero. Data-grid diagnostic indexes are data-column indexes, so
        // the visible identity is one position to the right in the DataTable.
        var previewColumnIndex = columnIndex + 1;
        return previewColumnIndex >= 0 && previewColumnIndex < PreviewTable.Columns.Count
            ? PreviewTable.Columns[previewColumnIndex].ColumnName
            : null;
    }
}

public readonly record struct ExcelGridNavigationRequest(
    int DisplayRowIndex,
    int ColumnIndex,
    string? ColumnIdentity);
