namespace KKL.WordStudio.Application.WorkingData;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Visitors;
using KKL.WordStudio.Shared.Results;

public readonly record struct WorkingDataCell(int RowIndex, int ColumnIndex);

public interface IWorksheetWorkingDataService
{
    Task<Result<Worksheet>> EnsureCreatedAsync(
        Project project,
        string workbookFilePath,
        string workbookFileName,
        string worksheetName,
        DataRange range,
        string? preferredDataSourceName = null,
        CancellationToken cancellationToken = default);

    ExcelDataSource? FindDataSource(Project project, string workbookFilePath);
    Worksheet? FindWorksheet(Project project, string workbookFilePath, string worksheetName);
    Result SetCell(Worksheet worksheet, int rowIndex, int columnIndex, string? value);
    Result ClearCells(Worksheet worksheet, IReadOnlyCollection<WorkingDataCell> cells);
    Result InsertRow(Worksheet worksheet, int rowIndex);
    Result DeleteRows(Worksheet worksheet, IReadOnlyCollection<int> rowIndexes);
    Result InsertColumn(Worksheet worksheet, int columnIndex);
    Result DeleteColumns(Project project, ExcelDataSource dataSource, Worksheet worksheet, IReadOnlyCollection<int> columnIndexes);
    Result ApplyClipboardMatrix(Worksheet worksheet, int startRowIndex, int startColumnIndex, string clipboardText);
    string BuildClipboardText(Worksheet worksheet, IReadOnlyCollection<WorkingDataCell> cells);
    void Reset(Worksheet worksheet);

    // -----------------------------------------------------------------
    // Sprint 11: undo/redo, find/replace
    // -----------------------------------------------------------------

    /// <summary>
    /// Runs a working-data mutation with worksheet-scoped undo support: a
    /// pre-mutation snapshot is captured, the mutation runs, and the snapshot is
    /// recorded as exactly one undo step only when the mutation succeeds. Failed
    /// mutations leave history untouched. This is the single funnel every
    /// content mutation (edit/clear/paste/insert/delete/replace) should use so
    /// each logical operation is one reversible step.
    /// </summary>
    Result Mutate(Worksheet worksheet, WorkingDataHistory history, Func<Result> mutation);

    /// <summary>Undoes the last recorded mutation for this worksheet.</summary>
    Result Undo(Worksheet worksheet, WorkingDataHistory history);

    /// <summary>Reapplies the last undone mutation for this worksheet.</summary>
    Result Redo(Worksheet worksheet, WorkingDataHistory history);

    /// <summary>
    /// Finds matches of <paramref name="query"/> in the worksheet's working data
    /// cells (case-insensitive contains). Header/column metadata is never
    /// searched. Read-only: never mutates data or creates working data.
    /// </summary>
    IReadOnlyList<WorkingDataCell> Find(Worksheet worksheet, string query);

    /// <summary>
    /// Replaces every case-insensitive occurrence of <paramref name="find"/>
    /// with <paramref name="replace"/> across working-data cell values only
    /// (never headers/metadata). Counts as one undo step. Returns the number of
    /// cells changed via <paramref name="replacedCount"/>.
    /// </summary>
    Result ReplaceAll(Worksheet worksheet, WorkingDataHistory history, string find, string replace, out int replacedCount);
}

/// <summary>
/// Coordinates lazy working-data creation and worksheet-scoped mutations.
/// It deliberately stays small: one ordered table snapshot, no formulas,
/// no source workbook writes, and no generic dataset engine.
/// </summary>
public sealed class WorksheetWorkingDataService : IWorksheetWorkingDataService
{
    private readonly IExcelWorkbookReader _excelReader;

    public WorksheetWorkingDataService(IExcelWorkbookReader excelReader) => _excelReader = excelReader;

    public async Task<Result<Worksheet>> EnsureCreatedAsync(
        Project project,
        string workbookFilePath,
        string workbookFileName,
        string worksheetName,
        DataRange range,
        string? preferredDataSourceName = null,
        CancellationToken cancellationToken = default)
    {
        var dataSource = FindDataSource(project, workbookFilePath);
        var worksheet = dataSource?.Workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, worksheetName, StringComparison.OrdinalIgnoreCase));
        if (worksheet?.WorkingData is not null)
            return Result.Success(worksheet);

        var readResult = await _excelReader.ReadWorkingDataAsync(
            workbookFilePath,
            worksheetName,
            range,
            cancellationToken);
        if (readResult.IsFailure)
            return Result.Failure<Worksheet>(readResult.Error!);

        dataSource ??= CreateDataSource(project, workbookFilePath, workbookFileName, worksheetName, preferredDataSourceName);
        dataSource.ActiveWorksheetName = worksheetName;
        if (worksheet is null)
        {
            worksheet = new Worksheet { Name = worksheetName };
            dataSource.Workbook.Worksheets.Add(worksheet);
        }

        worksheet.SelectedRange = CloneRange(range);
        worksheet.WorkingData = readResult.Value;
        return Result.Success(worksheet);
    }

    public ExcelDataSource? FindDataSource(Project project, string workbookFilePath) =>
        project.DataSources
            .OfType<ExcelDataSource>()
            .FirstOrDefault(ds => string.Equals(ds.Workbook.SourcePath, workbookFilePath, StringComparison.OrdinalIgnoreCase));

    public Worksheet? FindWorksheet(Project project, string workbookFilePath, string worksheetName) =>
        FindDataSource(project, workbookFilePath)?.Workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, worksheetName, StringComparison.OrdinalIgnoreCase));

    public Result SetCell(Worksheet worksheet, int rowIndex, int columnIndex, string? value)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");
        if (!IsValidCell(data, rowIndex, columnIndex)) return Result.Failure("Seçili hücre çalışma verisi aralığının dışında.");

        data.Rows[rowIndex].Values[columnIndex] = value;
        return Result.Success();
    }

    public Result ClearCells(Worksheet worksheet, IReadOnlyCollection<WorkingDataCell> cells)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");
        var distinctCells = cells.Distinct().ToList();

        if (distinctCells.Any(cell => !IsValidCell(data, cell.RowIndex, cell.ColumnIndex)))
            return Result.Failure("Seçili hücrelerden biri çalışma verisi aralığının dışında.");

        foreach (var cell in distinctCells)
            data.Rows[cell.RowIndex].Values[cell.ColumnIndex] = null;
        return Result.Success();
    }

    public Result InsertRow(Worksheet worksheet, int rowIndex)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");

        var insertAt = Math.Clamp(rowIndex, 0, data.Rows.Count);
        var row = new WorkingDataRow();
        for (var i = 0; i < data.Columns.Count; i++) row.Values.Add(null);
        data.Rows.Insert(insertAt, row);
        return Result.Success();
    }

    public Result DeleteRows(Worksheet worksheet, IReadOnlyCollection<int> rowIndexes)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");
        var indexes = rowIndexes.Distinct().OrderByDescending(i => i).ToList();
        if (indexes.Count == 0) return Result.Failure("Silinecek satır seçilmedi.");
        if (indexes.Any(i => i < 0 || i >= data.Rows.Count)) return Result.Failure("Seçili satırlardan biri çalışma verisi aralığının dışında.");

        foreach (var index in indexes) data.Rows.RemoveAt(index);
        return Result.Success();
    }

    public Result InsertColumn(Worksheet worksheet, int columnIndex)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");

        var insertAt = Math.Clamp(columnIndex, 0, data.Columns.Count);
        var field = $"WD_{Guid.NewGuid():N}";
        data.Columns.Insert(insertAt, new WorkingDataColumn
        {
            SourceField = field,
            Header = "Yeni Sütun"
        });
        foreach (var row in data.Rows) row.Values.Insert(insertAt, null);
        return Result.Success();
    }

    public Result DeleteColumns(Project project, ExcelDataSource dataSource, Worksheet worksheet, IReadOnlyCollection<int> columnIndexes)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");
        var indexes = columnIndexes.Distinct().OrderByDescending(i => i).ToList();
        if (indexes.Count == 0) return Result.Failure("Silinecek sütun seçilmedi.");
        if (indexes.Any(i => i < 0 || i >= data.Columns.Count)) return Result.Failure("Seçili sütunlardan biri çalışma verisi aralığının dışında.");

        foreach (var index in indexes)
        {
            var column = data.Columns[index];
            var aliases = ResolveColumnAliases(worksheet, column);
            var conflict = project.Reports
                .SelectMany(ReportElementFlattener.Flatten)
                .OfType<TableElement>()
                .FirstOrDefault(table =>
                    UsesColumnThroughLegacyBinding(table, dataSource, worksheet, aliases)
                    || UsesColumnThroughComposedSource(table, dataSource, worksheet, aliases));

            if (conflict is not null)
                return Result.Failure($"'{column.Header}' sütunu '{conflict.Name}' tablosu tarafından kullanılıyor; sütun silinemedi.");
        }

        foreach (var index in indexes)
        {
            var column = data.Columns[index];
            if (!string.IsNullOrWhiteSpace(column.OriginalSourceColumn))
            {
                worksheet.ColumnMappings.RemoveAll(mapping =>
                    string.Equals(mapping.SourceColumn, column.OriginalSourceColumn, StringComparison.OrdinalIgnoreCase));
            }

            data.Columns.RemoveAt(index);
            foreach (var row in data.Rows) row.Values.RemoveAt(index);
        }

        return Result.Success();
    }

    public Result ApplyClipboardMatrix(Worksheet worksheet, int startRowIndex, int startColumnIndex, string clipboardText)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");
        if (string.IsNullOrEmpty(clipboardText)) return Result.Failure("Panoda yapıştırılacak metin yok.");
        if (startRowIndex < 0 || startColumnIndex < 0)
            return Result.Failure("Yapıştırma başlangıcı geçersiz.");

        var matrix = ParseClipboardMatrix(clipboardText);
        if (matrix.Count == 0 || matrix.All(row => row.Count == 0))
            return Result.Failure("Panodaki veri okunamadı.");

        var requiredColumns = startColumnIndex + matrix.Max(row => row.Count);
        var requiredRows = startRowIndex + matrix.Count;

        // Auto-grow: append working-data columns first so every row has slots,
        // then append rows. New columns receive a stable identity and a unique
        // SourceField; they are project-only (OriginalSourceColumn stays null),
        // never rewrite the original XLSX/XLSM, and never mutate worksheet
        // ColumnMappings. The whole grow+paste is a single caller-level step.
        while (data.Columns.Count < requiredColumns)
        {
            data.Columns.Add(new WorkingDataColumn
            {
                SourceField = CreateUniqueSourceField(data),
                Header = "Yeni Sütun"
            });
            foreach (var row in data.Rows)
                while (row.Values.Count < data.Columns.Count) row.Values.Add(null);
        }

        while (data.Rows.Count < requiredRows)
        {
            var appended = new WorkingDataRow();
            for (var i = 0; i < data.Columns.Count; i++) appended.Values.Add(null);
            data.Rows.Add(appended);
        }

        // Existing rows may pre-date a column grow; normalise widths defensively.
        foreach (var row in data.Rows)
            while (row.Values.Count < data.Columns.Count) row.Values.Add(null);

        for (var rowOffset = 0; rowOffset < matrix.Count; rowOffset++)
        {
            var cells = matrix[rowOffset];
            for (var columnOffset = 0; columnOffset < cells.Count; columnOffset++)
                data.Rows[startRowIndex + rowOffset].Values[startColumnIndex + columnOffset] = cells[columnOffset];
        }

        return Result.Success();
    }

    public Result Mutate(Worksheet worksheet, WorkingDataHistory history, Func<Result> mutation)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");

        var before = WorkingDataSnapshot.Capture(data);
        var result = mutation();
        if (result.IsSuccess) history.Record(before);
        return result;
    }

    public Result Undo(Worksheet worksheet, WorkingDataHistory history)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");
        return history.Undo(data)
            ? Result.Success()
            : Result.Failure("Geri alınacak işlem yok.");
    }

    public Result Redo(Worksheet worksheet, WorkingDataHistory history)
    {
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");
        return history.Redo(data)
            ? Result.Success()
            : Result.Failure("Yinelenecek işlem yok.");
    }

    public IReadOnlyList<WorkingDataCell> Find(Worksheet worksheet, string query)
    {
        var data = worksheet.WorkingData;
        if (data is null || string.IsNullOrEmpty(query)) return Array.Empty<WorkingDataCell>();

        var matches = new List<WorkingDataCell>();
        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            var values = data.Rows[rowIndex].Values;
            for (var columnIndex = 0; columnIndex < values.Count; columnIndex++)
            {
                var value = values[columnIndex];
                if (value is not null && value.Contains(query, StringComparison.OrdinalIgnoreCase))
                    matches.Add(new WorkingDataCell(rowIndex, columnIndex));
            }
        }
        return matches;
    }

    public Result ReplaceAll(Worksheet worksheet, WorkingDataHistory history, string find, string replace, out int replacedCount)
    {
        replacedCount = 0;
        var data = worksheet.WorkingData;
        if (data is null) return Result.Failure("Çalışma verisi henüz oluşturulmadı.");
        if (string.IsNullOrEmpty(find)) return Result.Failure("Aranacak metin boş olamaz.");

        var replaced = 0;
        var result = Mutate(worksheet, history, () =>
        {
            foreach (var row in data.Rows)
            {
                for (var columnIndex = 0; columnIndex < row.Values.Count; columnIndex++)
                {
                    var value = row.Values[columnIndex];
                    if (value is null) continue;
                    var updated = ReplaceCaseInsensitive(value, find, replace);
                    if (!string.Equals(updated, value, StringComparison.Ordinal))
                    {
                        row.Values[columnIndex] = updated;
                        replaced++;
                    }
                }
            }
            return replaced > 0 ? Result.Success() : Result.Failure("Eşleşme bulunamadı.");
        });

        replacedCount = replaced;
        return result;
    }

    public string BuildClipboardText(Worksheet worksheet, IReadOnlyCollection<WorkingDataCell> cells)
    {
        var data = worksheet.WorkingData;
        if (data is null || cells.Count == 0) return string.Empty;

        var valid = cells.Where(cell => IsValidCell(data, cell.RowIndex, cell.ColumnIndex)).Distinct().ToList();
        if (valid.Count == 0) return string.Empty;

        var minRow = valid.Min(cell => cell.RowIndex);
        var maxRow = valid.Max(cell => cell.RowIndex);
        var minColumn = valid.Min(cell => cell.ColumnIndex);
        var maxColumn = valid.Max(cell => cell.ColumnIndex);
        var selected = valid.ToHashSet();
        var lines = new List<string>();

        for (var rowIndex = minRow; rowIndex <= maxRow; rowIndex++)
        {
            var values = new List<string>();
            for (var columnIndex = minColumn; columnIndex <= maxColumn; columnIndex++)
            {
                var cell = new WorkingDataCell(rowIndex, columnIndex);
                values.Add(selected.Contains(cell) ? data.Rows[rowIndex].Values[columnIndex] ?? string.Empty : string.Empty);
            }
            lines.Add(string.Join('\t', values));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public void Reset(Worksheet worksheet) => worksheet.WorkingData = null;


    private static bool UsesColumnThroughLegacyBinding(
        TableElement table,
        ExcelDataSource dataSource,
        Worksheet worksheet,
        HashSet<string> aliases) =>
        table.Sources.Count == 0
        && string.Equals(table.Binding?.DataSourceName, dataSource.Name, StringComparison.OrdinalIgnoreCase)
        && string.Equals(table.Binding?.WorksheetName ?? dataSource.ActiveWorksheetName, worksheet.Name, StringComparison.OrdinalIgnoreCase)
        && table.Columns.Any(tableColumn => tableColumn.SourceField is { } sourceField && aliases.Contains(sourceField));

    private static bool UsesColumnThroughComposedSource(
        TableElement table,
        ExcelDataSource dataSource,
        Worksheet worksheet,
        HashSet<string> aliases) =>
        table.Sources.Any(source =>
            string.Equals(source.DataSourceName, dataSource.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(source.WorksheetName, worksheet.Name, StringComparison.OrdinalIgnoreCase)
            && source.FieldMappings.Any(mapping => aliases.Contains(mapping.SourceField)));

    private static string CreateUniqueSourceField(WorksheetWorkingData data)
    {
        // Stable, unique, non-colliding project-only field name. Guid keeps it
        // unique against any existing source/original letters and other inserts.
        string field;
        do
        {
            field = $"WD_{Guid.NewGuid():N}";
        }
        while (data.Columns.Any(column => string.Equals(column.SourceField, field, StringComparison.OrdinalIgnoreCase)));
        return field;
    }

    private static string ReplaceCaseInsensitive(string source, string find, string replace)
    {
        var builder = new System.Text.StringBuilder();
        var index = 0;
        while (index < source.Length)
        {
            var match = source.IndexOf(find, index, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
            {
                builder.Append(source, index, source.Length - index);
                break;
            }
            builder.Append(source, index, match - index);
            builder.Append(replace);
            index = match + find.Length;
        }
        return builder.ToString();
    }

    private static bool IsValidCell(WorksheetWorkingData data, int rowIndex, int columnIndex) =>
        rowIndex >= 0 && rowIndex < data.Rows.Count && columnIndex >= 0 && columnIndex < data.Columns.Count;

    private static HashSet<string> ResolveColumnAliases(Worksheet worksheet, WorkingDataColumn column)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { column.SourceField };
        if (!string.IsNullOrWhiteSpace(column.OriginalSourceColumn))
            aliases.Add(column.OriginalSourceColumn);
        var mapping = worksheet.ColumnMappings.FirstOrDefault(candidate =>
            string.Equals(candidate.SourceColumn, column.OriginalSourceColumn ?? column.SourceField, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.SourceColumn, column.SourceField, StringComparison.OrdinalIgnoreCase));
        if (mapping is not null) aliases.Add(mapping.TargetField.Name);
        return aliases;
    }

    private static List<IReadOnlyList<string?>> ParseClipboardMatrix(string clipboardText)
    {
        var normalized = clipboardText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines.Select(line => (IReadOnlyList<string?>)line.Split('\t').Cast<string?>().ToList()).ToList();
    }

    private static ExcelDataSource CreateDataSource(
        Project project,
        string workbookFilePath,
        string workbookFileName,
        string worksheetName,
        string? preferredDataSourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(preferredDataSourceName)
            ? Path.GetFileNameWithoutExtension(workbookFileName)
            : preferredDataSourceName.Trim();
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Veri Kaynağı";

        var name = baseName;
        var suffix = 2;
        while (project.DataSources.Any(ds => string.Equals(ds.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = $"{baseName} {suffix++}";

        var dataSource = new ExcelDataSource
        {
            Name = name,
            Workbook = new Workbook { FileName = workbookFileName, SourcePath = workbookFilePath },
            ActiveWorksheetName = worksheetName
        };
        project.DataSources.Add(dataSource);
        return dataSource;
    }

    private static DataRange CloneRange(DataRange range) => new()
    {
        DataStartRow = range.DataStartRow,
        DataEndRow = range.DataEndRow,
        HeaderRowIndex = range.HeaderRowIndex,
        StartColumn = range.StartColumn,
        EndColumn = range.EndColumn,
        WasAutoDetected = range.WasAutoDetected
    };
}
