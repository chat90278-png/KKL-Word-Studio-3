namespace KKL.WordStudio.UI.ViewModels;

using System.Globalization;
using System.Text;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;

public sealed partial class ExcelWorkspaceViewModel
{
    public event Action<ExcelGridNavigationRequest>? DiagnosticGridNavigationRequested;

    public Task<bool> NavigateToDiagnosticSourceAsync(
        PreviewDiagnosticSource source,
        string? keyValue) =>
        NavigateToDiagnosticSourceAsync(
            source,
            string.IsNullOrWhiteSpace(keyValue) ? Array.Empty<string>() : [keyValue],
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

            var preferredMatches = expectedKeyColumnIndex >= 0
                ? matches.Where(candidate => candidate.ColumnIndex == expectedKeyColumnIndex).ToList()
                : [];
            var keyMatch = preferredMatches.Count > 0 ? preferredMatches[0] : matches[0];
            var targetColumnIndex = affectedColumnIndex >= 0
                ? affectedColumnIndex
                : keyMatch.ColumnIndex;

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

        StatusText = "Uyarının kayıt anahtarı kaynak veride bulunamadı.";
        return false;
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

        var headerPreviewIndex = _currentPreview.RowNumbers.IndexOf(headerRowNumber);
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

    private static bool DiagnosticColumnMatches(string requested, string? candidate)
    {
        var requestedIdentity = NormalizeDiagnosticColumn(requested);
        var candidateIdentity = NormalizeDiagnosticColumn(candidate);
        if (requestedIdentity.Length == 0 || candidateIdentity.Length == 0)
            return false;
        if (string.Equals(requestedIdentity, candidateIdentity, StringComparison.Ordinal))
            return true;

        return IsSameAliasGroup(requestedIdentity, candidateIdentity, QuantityAliases)
            || IsSameAliasGroup(requestedIdentity, candidateIdentity, SerialAliases)
            || IsSameAliasGroup(requestedIdentity, candidateIdentity, MatchKeyAliases);
    }

    private static bool IsSameAliasGroup(string requested, string candidate, IReadOnlySet<string> aliases) =>
        aliases.Contains(requested) && aliases.Contains(candidate);

    private static string NormalizeDiagnosticColumn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var lower = char.ToLowerInvariant(character);
            if (lower == 'ı') lower = 'i';
            if (char.IsLetterOrDigit(lower))
                builder.Append(lower);
        }

        return builder.ToString();
    }

    private static readonly IReadOnlySet<string> QuantityAliases = new HashSet<string>(StringComparer.Ordinal)
    {
        "adet", "miktar", "quantity", "qty"
    };

    private static readonly IReadOnlySet<string> SerialAliases = new HashSet<string>(StringComparer.Ordinal)
    {
        "serino", "serinumarasi", "serialno", "serialnumber", "sn"
    };

    private static readonly IReadOnlySet<string> MatchKeyAliases = new HashSet<string>(StringComparer.Ordinal)
    {
        "pn", "partno", "partnumber", "productno", "productnumber", "parcanumarasi", "urunno"
    };

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
