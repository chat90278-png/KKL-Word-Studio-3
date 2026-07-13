namespace KKL.WordStudio.Infrastructure.Excel;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Domain.DataSources;
using DomainWorkbook = KKL.WordStudio.Domain.DataSources.Workbook;
using DomainWorksheet = KKL.WordStudio.Domain.DataSources.Worksheet;
using KKL.WordStudio.Shared.Results;
using KKL.WordStudio.Shared.Spreadsheet;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reads .xlsx and .xlsm OpenXML workbooks using the OpenXML SDK — the same package already
/// planned for Word export, so no extra dependency was introduced to
/// support Excel import. Read-only: this class never writes to the
/// workbook, matching the Sprint 2 scope ("henüz editing gerekmiyor").
///
/// Uses explicit DomainWorkbook/DomainWorksheet aliases because
/// DocumentFormat.OpenXml.Spreadsheet also defines Workbook/Worksheet
/// types — without the alias, "Workbook"/"Worksheet" is ambiguous between
/// the OpenXML SDK's types and our own Domain types (a real build-breaking
/// error caught during Sprint 5 stabilization; preserved during the
/// Sprint 6 integration).
/// </summary>
public sealed class OpenXmlExcelWorkbookReader : IExcelWorkbookReader
{
    private readonly ILogger<OpenXmlExcelWorkbookReader> _logger;

    public OpenXmlExcelWorkbookReader(ILogger<OpenXmlExcelWorkbookReader> logger) => _logger = logger;

    public Task<Result<DomainWorkbook>> OpenWorkbookAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => OpenWorkbook(filePath, cancellationToken), cancellationToken);

    private Result<DomainWorkbook> OpenWorkbook(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
                return Result.Failure<DomainWorkbook>($"Excel dosyası bulunamadı: {filePath}");

            using var document = SpreadsheetDocument.Open(filePath, false);
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException("The workbook has no WorkbookPart.");

            var workbook = new DomainWorkbook
            {
                FileName = Path.GetFileName(filePath),
                SourcePath = filePath
            };

            var sheets = workbookPart.Workbook.Sheets?.Elements<Sheet>() ?? Enumerable.Empty<Sheet>();
            foreach (var sheet in sheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (sheet.Name is null) continue;
                workbook.Worksheets.Add(new DomainWorksheet { Name = sheet.Name.Value! });
            }

            return Result.Success(workbook);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open workbook {FilePath}", filePath);
            return Result.Failure<DomainWorkbook>(
                $"Excel dosyası açılamadı: '{Path.GetFileName(filePath)}'. Geçerli bir .xlsx veya .xlsm dosyası olduğundan ve başka bir programda açık olmadığından emin olun.");
        }
    }

    public Task<Result<SheetPreview>> GetSheetPreviewAsync(
        string filePath,
        string worksheetName,
        int maxPreviewRows = 100,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => GetSheetPreview(filePath, worksheetName, maxPreviewRows, cancellationToken),
            cancellationToken);

    private Result<SheetPreview> GetSheetPreview(
        string filePath,
        string worksheetName,
        int maxPreviewRows,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = SpreadsheetDocument.Open(filePath, false);
            var (worksheetPart, sharedStrings) = OpenWorksheetPart(document, worksheetName);

            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
            var rowNumbers = new List<int>();
            var rows = new List<IReadOnlyList<string>>();
            var maxColumnCount = 0;
            var truncated = false;
            var readCount = 0;

            foreach (var row in sheetData.Elements<Row>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (readCount >= maxPreviewRows)
                {
                    truncated = true;
                    break;
                }

                var rowIndex = (int)(row.RowIndex?.Value ?? (uint)(readCount + 1));
                var cellsInRow = ReadRowCells(row, sharedStrings);

                rowNumbers.Add(rowIndex);
                rows.Add(cellsInRow);
                maxColumnCount = Math.Max(maxColumnCount, cellsInRow.Count);
                readCount++;
            }

            var preview = new SheetPreview
            {
                WorksheetName = worksheetName,
                RowNumbers = rowNumbers,
                Rows = rows,
                ColumnCount = maxColumnCount,
                IsTruncated = truncated
            };

            return Result.Success(preview);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview sheet {Sheet} in {FilePath}", worksheetName, filePath);
            return Result.Failure<SheetPreview>(
                $"'{worksheetName}' sayfası okunamadı. Sayfa yeniden adlandırılmış veya silinmiş olabilir — dosyayı yeniden açmayı deneyin.");
        }
    }

    public Task<Result<WorksheetWorkingData>> ReadWorkingDataAsync(
        string filePath,
        string worksheetName,
        DataRange range,
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => ReadWorkingData(filePath, worksheetName, range, cancellationToken),
            cancellationToken);

    private Result<WorksheetWorkingData> ReadWorkingData(
        string filePath,
        string worksheetName,
        DataRange range,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (range.DataEndRow is null || range.DataEndRow < range.DataStartRow)
                return Result.Failure<WorksheetWorkingData>("Çalışma verisi için geçerli bir veri aralığı gerekli.");

            using var document = SpreadsheetDocument.Open(filePath, false);
            var (worksheetPart, sharedStrings) = OpenWorksheetPart(document, worksheetName);
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

            var startColumn = range.StartColumn ?? 1;
            var explicitEndColumn = range.EndColumn;
            var discoveredEndColumn = explicitEndColumn ?? 0;
            var headerValues = new Dictionary<int, string>();
            var bufferedRows = new List<(int RowIndex, Dictionary<int, string> Values)>();

            var firstRelevantRow = Math.Min(range.HeaderRowIndex ?? range.DataStartRow, range.DataStartRow);
            var lastRelevantRow = Math.Max(range.HeaderRowIndex ?? range.DataEndRow.Value, range.DataEndRow.Value);

            foreach (var row in sheetData.Elements<Row>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rowIndex = (int)(row.RowIndex?.Value ?? 0);
                if (rowIndex < firstRelevantRow) continue;
                if (rowIndex > lastRelevantRow) break;

                var isHeaderRow = range.HeaderRowIndex == rowIndex;
                var isDataRow = rowIndex >= range.DataStartRow && rowIndex <= range.DataEndRow.Value;
                if (!isHeaderRow && !isDataRow) continue;

                var valuesByColumn = ReadRowCellsByColumn(row, sharedStrings);
                if (!explicitEndColumn.HasValue && valuesByColumn.Count > 0)
                    discoveredEndColumn = Math.Max(discoveredEndColumn, valuesByColumn.Keys.Max());

                if (isHeaderRow)
                    headerValues = valuesByColumn;

                if (isDataRow)
                    bufferedRows.Add((rowIndex, valuesByColumn));
            }

            var endColumn = explicitEndColumn ?? discoveredEndColumn;
            if (endColumn < startColumn)
                return Result.Failure<WorksheetWorkingData>("Çalışma verisi için geçerli bir sütun aralığı bulunamadı.");

            var workingData = new WorksheetWorkingData();
            for (var columnIndex = startColumn; columnIndex <= endColumn; columnIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var letter = ColumnLetterConverter.ToLetters(columnIndex);
                var header = headerValues.TryGetValue(columnIndex, out var headerText) && !string.IsNullOrWhiteSpace(headerText)
                    ? headerText.Trim()
                    : $"Sütun {letter}";
                workingData.Columns.Add(new WorkingDataColumn
                {
                    SourceField = letter,
                    Header = header,
                    OriginalSourceColumn = letter
                });
            }

            foreach (var bufferedRow in bufferedRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var workingRow = new WorkingDataRow { OriginalRowNumber = bufferedRow.RowIndex };
                for (var columnIndex = startColumn; columnIndex <= endColumn; columnIndex++)
                    workingRow.Values.Add(bufferedRow.Values.TryGetValue(columnIndex, out var value) ? value : null);
                workingData.Rows.Add(workingRow);
            }

            return Result.Success(workingData);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read working data for sheet {Sheet} in {FilePath}", worksheetName, filePath);
            return Result.Failure<WorksheetWorkingData>(
                $"'{worksheetName}' sayfasının çalışma verisi hazırlanamadı. Kaynak Excel dosyasının erişilebilir olduğundan emin olun.");
        }
    }

    public Task<Result<DataRange>> DetectDataRangeAsync(
        string filePath,
        string worksheetName,
        int dataStartRow,
        CancellationToken cancellationToken = default) =>
        DetectDataRangeCoreAsync(
            filePath,
            worksheetName,
            dataStartRow,
            startColumn: null,
            endColumn: null,
            cancellationToken);

    public Task<Result<DataRange>> DetectDataRangeAsync(
        string filePath,
        string worksheetName,
        int dataStartRow,
        int startColumn,
        int endColumn,
        CancellationToken cancellationToken = default) =>
        DetectDataRangeCoreAsync(
            filePath,
            worksheetName,
            dataStartRow,
            startColumn,
            endColumn,
            cancellationToken);

    private Task<Result<DataRange>> DetectDataRangeCoreAsync(
        string filePath,
        string worksheetName,
        int dataStartRow,
        int? startColumn,
        int? endColumn,
        CancellationToken cancellationToken) =>
        Task.Run(
            () => DetectDataRangeCore(
                filePath,
                worksheetName,
                dataStartRow,
                startColumn,
                endColumn,
                cancellationToken),
            cancellationToken);

    private Result<DataRange> DetectDataRangeCore(
        string filePath,
        string worksheetName,
        int dataStartRow,
        int? startColumn,
        int? endColumn,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var document = SpreadsheetDocument.Open(filePath, false);
            var (worksheetPart, sharedStrings) = OpenWorksheetPart(document, worksheetName);
            var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

            int? lastNonBlankRow = null;
            var minColumn = int.MaxValue;
            var maxColumn = 0;
            var sawBlankAfterData = false;

            foreach (var row in sheetData.Elements<Row>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rowIndex = (int)(row.RowIndex?.Value ?? 0);
                if (rowIndex < dataStartRow) continue;
                if (sawBlankAfterData) break;
                if (lastNonBlankRow.HasValue && rowIndex > lastNonBlankRow.Value + 1) break;

                var valuesByColumn = ReadRowCellsByColumn(row, sharedStrings);
                var occupiedColumns = valuesByColumn
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                    .Select(pair => pair.Key)
                    .Where(column => (!startColumn.HasValue || column >= startColumn.Value)
                                     && (!endColumn.HasValue || column <= endColumn.Value))
                    .ToList();
                var hasAnyValue = occupiedColumns.Count > 0;

                if (!hasAnyValue)
                {
                    if (lastNonBlankRow.HasValue) sawBlankAfterData = true;
                    continue;
                }

                lastNonBlankRow = rowIndex;
                minColumn = Math.Min(minColumn, occupiedColumns.Min());
                maxColumn = Math.Max(maxColumn, occupiedColumns.Max());
            }

            var range = new DataRange
            {
                DataStartRow = dataStartRow,
                DataEndRow = lastNonBlankRow,
                StartColumn = startColumn ?? (minColumn == int.MaxValue ? null : minColumn),
                EndColumn = endColumn ?? (maxColumn == 0 ? null : maxColumn),
                WasAutoDetected = true
            };

            return Result.Success(range);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect data range in {Sheet} of {FilePath}", worksheetName, filePath);
            return Result.Failure<DataRange>(
                "Veri aralığı otomatik olarak algılanamadı. Aralığı elle ayarlamayı deneyin.");
        }
    }

    private static (WorksheetPart Part, SharedStringTable? SharedStrings) OpenWorksheetPart(
        SpreadsheetDocument document,
        string worksheetName)
    {
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException("The workbook has no WorkbookPart.");

        var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>()
            .FirstOrDefault(s => s.Name == worksheetName)
            ?? throw new InvalidDataException($"Worksheet '{worksheetName}' was not found.");

        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        return (worksheetPart, sharedStrings);
    }

    private static Dictionary<int, string> ReadRowCellsByColumn(
        Row row,
        SharedStringTable? sharedStrings)
    {
        var cells = new Dictionary<int, string>();
        var nextColumn = 1;
        foreach (var cell in row.Elements<Cell>())
        {
            var columnIndex = ResolveCellColumn(cell, nextColumn);
            cells[columnIndex] = GetCellText(cell, sharedStrings);
            nextColumn = columnIndex + 1;
        }
        return cells;
    }

    private static IReadOnlyList<string> ReadRowCells(Row row, SharedStringTable? sharedStrings)
    {
        var byColumn = ReadRowCellsByColumn(row, sharedStrings);
        if (byColumn.Count == 0) return Array.Empty<string>();

        var cells = Enumerable.Repeat(string.Empty, byColumn.Keys.Max()).ToList();
        foreach (var pair in byColumn) cells[pair.Key - 1] = pair.Value;
        return cells;
    }

    private static int ResolveCellColumn(Cell cell, int fallbackColumn)
    {
        if (cell.CellReference?.Value is not { } reference) return fallbackColumn;
        var (letters, _) = ColumnLetterConverter.SplitCellReference(reference);
        return ColumnLetterConverter.ToIndex(letters);
    }

    private static string GetCellText(Cell cell, SharedStringTable? sharedStrings)
    {
        var rawValue = cell.CellValue?.Text ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            if (sharedStrings is null || !int.TryParse(rawValue, out var sharedStringIndex))
                return string.Empty;

            var sharedItem = sharedStrings.ElementAtOrDefault(sharedStringIndex);
            return sharedItem?.InnerText ?? string.Empty;
        }

        if (cell.DataType?.Value == CellValues.Boolean)
            return rawValue == "1" ? "TRUE" : "FALSE";

        return rawValue;
    }
}
