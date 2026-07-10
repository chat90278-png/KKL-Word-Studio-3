namespace KKL.WordStudio.Infrastructure.Excel;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Shared.Results;
using KKL.WordStudio.Shared.Spreadsheet;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads configured Excel worksheet rows for report binding. Sprint 9 gives
/// worksheet WorkingData strict precedence; when no working snapshot exists,
/// the original XLSX/XLSM is opened read-only exactly as before.
/// </summary>
public sealed class ExcelDataProvider : IDataProvider
{
    private readonly ILogger<ExcelDataProvider> _logger;

    public ExcelDataProvider(ILogger<ExcelDataProvider> logger) => _logger = logger;

    public string ProviderKey => "excel";

    public Task<Result<IReadOnlyList<IReadOnlyDictionary<string, object?>>>> GetRowsAsync(
        IDataSourceDefinition definition, CancellationToken cancellationToken = default, string? worksheetNameOverride = null, DataRange? rangeOverride = null)
    {
        if (definition is not ExcelDataSource excelDataSource)
            return Task.FromResult(Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                $"ExcelDataProvider cannot read a data source of type '{definition.GetType().Name}'."));

        var worksheetName = worksheetNameOverride ?? excelDataSource.ActiveWorksheetName;
        var worksheet = excelDataSource.Workbook.Worksheets.FirstOrDefault(w => w.Name == worksheetName);
        if (worksheet is null)
            return Task.FromResult(Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                "Seçili Excel sayfası proje veri kaynağında bulunamadı."));

        var range = rangeOverride ?? worksheet.SelectedRange;
        if (range is null)
            return Task.FromResult(Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                "Seçili Excel sayfası için veri aralığı yapılandırılmamış."));

        var mappings = worksheet.ColumnMappings.Count > 0
            ? worksheet.ColumnMappings
            : excelDataSource.ColumnMappings; // pre-Sprint-8 project fallback

        if (worksheet.WorkingData is not null)
            return Task.FromResult(Result.Success<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                BuildWorkingDataRows(worksheet.WorkingData, mappings)));

        var sourcePath = excelDataSource.Workbook.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return Task.FromResult(Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                $"Excel kaynak dosyası bulunamadı: '{sourcePath}'. Kaynak çalışma verisi varsa proje içinden kullanılabilir; kaynak yenileme için dosya yeniden erişilebilir olmalıdır."));

        try
        {
            using var document = SpreadsheetDocument.Open(sourcePath, false);
            var workbookPart = document.WorkbookPart ?? throw new InvalidDataException("The workbook has no WorkbookPart.");
            var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault(s => s.Name == worksheetName)
                ?? throw new InvalidDataException($"Worksheet '{worksheetName}' was not found.");
            var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

            var rows = new List<IReadOnlyDictionary<string, object?>>();

            foreach (var row in sheetData.Elements<Row>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowIndex = (int)(row.RowIndex?.Value ?? 0);
                if (rowIndex < range.DataStartRow) continue;
                if (range.DataEndRow.HasValue && rowIndex > range.DataEndRow.Value) break;

                var cellValuesByColumn = new Dictionary<int, string>();
                foreach (var cell in row.Elements<Cell>())
                {
                    if (cell.CellReference?.Value is null) continue;
                    var (letters, _) = ColumnLetterConverter.SplitCellReference(cell.CellReference.Value);
                    cellValuesByColumn[ColumnLetterConverter.ToIndex(letters)] = GetCellText(cell, sharedStrings);
                }

                var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var mapping in mappings)
                {
                    var columnIndex = ColumnLetterConverter.ToIndex(mapping.SourceColumn);
                    record[mapping.TargetField.Name] = cellValuesByColumn.TryGetValue(columnIndex, out var value) ? value : null;
                }

                foreach (var (columnIndex, cellText) in cellValuesByColumn)
                {
                    if (range.StartColumn.HasValue && columnIndex < range.StartColumn.Value) continue;
                    if (range.EndColumn.HasValue && columnIndex > range.EndColumn.Value) continue;
                    record.TryAdd(ColumnLetterConverter.ToLetters(columnIndex), cellText);
                }

                rows.Add(record);
            }

            return Task.FromResult(Result.Success<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(rows));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read rows for data source {DataSource} from {Path}", excelDataSource.Name, sourcePath);
            return Task.FromResult(Result.Failure<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                $"'{excelDataSource.Name}' kaynağından veri okunamadı. Excel dosyasının başka bir programda açık olmadığından ve taşınmadığından emin olup yeniden deneyin."));
        }
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> BuildWorkingDataRows(
        WorksheetWorkingData workingData,
        IReadOnlyList<ColumnMapping> mappings)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>(workingData.Rows.Count);
        foreach (var row in workingData.Rows)
        {
            var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var columnIndex = 0; columnIndex < workingData.Columns.Count; columnIndex++)
            {
                var column = workingData.Columns[columnIndex];
                var value = columnIndex < row.Values.Count ? row.Values[columnIndex] : null;
                record[column.SourceField] = value;

                if (!string.IsNullOrWhiteSpace(column.OriginalSourceColumn))
                    record[column.OriginalSourceColumn] = value;
                var mapping = mappings.FirstOrDefault(candidate =>
                    string.Equals(candidate.SourceColumn, column.OriginalSourceColumn ?? column.SourceField, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.SourceColumn, column.SourceField, StringComparison.OrdinalIgnoreCase));
                if (mapping is not null)
                    record[mapping.TargetField.Name] = value;
            }
            rows.Add(record);
        }
        return rows;
    }

    private static string GetCellText(Cell cell, SharedStringTable? sharedStrings)
    {
        var rawValue = cell.CellValue?.Text ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (sharedStrings is null || !int.TryParse(rawValue, out var index)) return string.Empty;
            return sharedStrings.ElementAtOrDefault(index)?.InnerText ?? string.Empty;
        }

        if (cell.DataType?.Value == CellValues.Boolean)
            return rawValue == "1" ? "TRUE" : "FALSE";

        return rawValue;
    }
}
