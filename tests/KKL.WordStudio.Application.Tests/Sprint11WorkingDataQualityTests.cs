namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;
using Xunit;

public class Sprint11WorkingDataQualityTests
{
    // ---------------------------------------------------------------
    // Undo / Redo
    // ---------------------------------------------------------------

    [Fact]
    public async Task Undo_CellEdit_RestoresPreviousValue()
    {
        var (service, worksheet, history) = await SetupAsync();
        var original = worksheet.WorkingData!.Rows[0].Values[0];

        AssertOk(service.Mutate(worksheet, history, () => service.SetCell(worksheet, 0, 0, "Changed")));
        Assert.Equal("Changed", worksheet.WorkingData.Rows[0].Values[0]);

        AssertOk(service.Undo(worksheet, history));
        Assert.Equal(original, worksheet.WorkingData.Rows[0].Values[0]);
    }

    [Fact]
    public async Task Redo_ReappliesUndoneMutation()
    {
        var (service, worksheet, history) = await SetupAsync();

        AssertOk(service.Mutate(worksheet, history, () => service.SetCell(worksheet, 0, 0, "Changed")));
        AssertOk(service.Undo(worksheet, history));
        AssertOk(service.Redo(worksheet, history));

        Assert.Equal("Changed", worksheet.WorkingData!.Rows[0].Values[0]);
    }

    [Fact]
    public async Task NewMutationAfterUndo_ClearsRedo()
    {
        var (service, worksheet, history) = await SetupAsync();

        AssertOk(service.Mutate(worksheet, history, () => service.SetCell(worksheet, 0, 0, "First")));
        AssertOk(service.Undo(worksheet, history));

        // A new mutation after undo must discard the previously undone future.
        AssertOk(service.Mutate(worksheet, history, () => service.SetCell(worksheet, 0, 0, "Second")));

        Assert.False(history.CanRedo);
        Assert.True(service.Redo(worksheet, history).IsFailure);
        Assert.Equal("Second", worksheet.WorkingData!.Rows[0].Values[0]);
    }

    [Fact]
    public async Task UndoHistory_IsIsolatedPerWorksheet()
    {
        var service = new WorksheetWorkingDataService(new FakeReader());
        var registry = new WorkingDataHistoryRegistry();
        var project = new Project();
        var sheet1 = AssertWorksheet(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet1", Range()));
        var sheet2 = AssertWorksheet(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet2", Range()));

        AssertOk(service.Mutate(sheet1, registry.For(sheet1), () => service.SetCell(sheet1, 0, 0, "Only1")));

        // Sheet2's history is separate and empty; undoing it must not touch Sheet1.
        Assert.False(registry.For(sheet2).CanUndo);
        Assert.True(service.Undo(sheet2, registry.For(sheet2)).IsFailure);
        Assert.Equal("Only1", sheet1.WorkingData!.Rows[0].Values[0]);

        // Sheet1 retains its own independent, undoable step.
        Assert.True(registry.For(sheet1).CanUndo);
        AssertOk(service.Undo(sheet1, registry.For(sheet1)));
        Assert.Equal("Sheet1-R1", sheet1.WorkingData.Rows[0].Values[0]);
    }

    [Fact]
    public async Task FailedMutation_CreatesNoHistoryEntry()
    {
        var (service, worksheet, history) = await SetupAsync();

        var result = service.Mutate(worksheet, history, () => service.SetCell(worksheet, 999, 0, "x"));

        Assert.True(result.IsFailure);
        Assert.False(history.CanUndo);
    }

    // ---------------------------------------------------------------
    // Paste auto-grow
    // ---------------------------------------------------------------

    [Fact]
    public async Task PasteAutoGrow_AddsRequiredRowsAndColumns()
    {
        var (service, worksheet, _) = await SetupAsync();
        var data = worksheet.WorkingData!;
        Assert.Equal(2, data.Rows.Count);
        Assert.Equal(2, data.Columns.Count);

        // 3x3 paste starting at (1,1) needs rows up to index 3 and cols up to 3.
        AssertOk(service.ApplyClipboardMatrix(worksheet, 1, 1, "a\tb\tc\r\nd\te\tf\r\ng\th\ti"));

        Assert.Equal(4, data.Rows.Count);
        Assert.Equal(4, data.Columns.Count);
        Assert.Equal("i", data.Rows[3].Values[3]);
        // New columns carry stable identity and unique, non-empty SourceField.
        Assert.All(data.Columns, column => Assert.False(string.IsNullOrEmpty(column.SourceField)));
        Assert.Equal(data.Columns.Select(c => c.SourceField).Distinct().Count(), data.Columns.Count);
        Assert.Equal(data.Columns.Select(c => c.Id).Distinct().Count(), data.Columns.Count);
        // Auto-grown columns are project-only, leaving mappings untouched.
        Assert.Empty(worksheet.ColumnMappings);
    }

    [Fact]
    public async Task PasteAutoGrow_IsSingleUndoStep()
    {
        var (service, worksheet, history) = await SetupAsync();
        var data = worksheet.WorkingData!;

        AssertOk(service.Mutate(worksheet, history,
            () => service.ApplyClipboardMatrix(worksheet, 1, 1, "a\tb\tc\r\nd\te\tf\r\ng\th\ti")));

        // One paste == one undo step: a single Undo reverts the whole grow+paste.
        AssertOk(service.Undo(worksheet, history));
        Assert.Equal(2, data.Rows.Count);
        Assert.Equal(2, data.Columns.Count);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public async Task PasteAutoGrow_HandlesRaggedRowsAndTrailingNewline()
    {
        var (service, worksheet, _) = await SetupAsync();
        var data = worksheet.WorkingData!;

        // Ragged (second row shorter), LF endings, trailing newline.
        AssertOk(service.ApplyClipboardMatrix(worksheet, 0, 0, "x\ty\nz\n"));

        Assert.Equal("x", data.Rows[0].Values[0]);
        Assert.Equal("y", data.Rows[0].Values[1]);
        Assert.Equal("z", data.Rows[1].Values[0]);
        // No phantom row appended from the trailing newline.
        Assert.Equal(2, data.Rows.Count);
    }

    // ---------------------------------------------------------------
    // Find / Replace
    // ---------------------------------------------------------------

    [Fact]
    public async Task Find_IsCaseInsensitiveAndDoesNotMutateData()
    {
        var (service, worksheet, _) = await SetupAsync();
        AssertOk(service.SetCell(worksheet, 0, 0, "Hello World"));
        var before = Snapshot(worksheet.WorkingData!);

        var matches = service.Find(worksheet, "hello");

        Assert.Single(matches);
        Assert.Equal(new WorkingDataCell(0, 0), matches[0]);
        Assert.Equal(before, Snapshot(worksheet.WorkingData!));
    }

    [Fact]
    public async Task ReplaceAll_IsSingleUndoStep()
    {
        var (service, worksheet, history) = await SetupAsync();
        var data = worksheet.WorkingData!;
        AssertOk(service.SetCell(worksheet, 0, 0, "cat"));
        AssertOk(service.SetCell(worksheet, 1, 0, "cat"));
        history.Clear();

        AssertOk(service.ReplaceAll(worksheet, history, "cat", "dog", out var replaced));
        Assert.Equal(2, replaced);
        Assert.Equal("dog", data.Rows[0].Values[0]);
        Assert.Equal("dog", data.Rows[1].Values[0]);

        // Replace All is one undo step covering every replaced cell.
        AssertOk(service.Undo(worksheet, history));
        Assert.Equal("cat", data.Rows[0].Values[0]);
        Assert.Equal("cat", data.Rows[1].Values[0]);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public async Task Replace_DoesNotTouchHeadersOrColumnMetadata()
    {
        var (service, worksheet, history) = await SetupAsync();
        var data = worksheet.WorkingData!;
        var headerBefore = data.Columns[0].Header; // "Name"

        // "Name" only appears in header metadata, not in cell values.
        var result = service.ReplaceAll(worksheet, history, "Name", "XXX", out var replaced);

        Assert.True(result.IsFailure); // no cell match
        Assert.Equal(0, replaced);
        Assert.Equal(headerBefore, data.Columns[0].Header);
    }

    // ---------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------

    private static async Task<(WorksheetWorkingDataService service, Worksheet worksheet, WorkingDataHistory history)> SetupAsync()
    {
        var service = new WorksheetWorkingDataService(new FakeReader());
        var project = new Project();
        var worksheet = AssertWorksheet(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet1", Range()));
        return (service, worksheet, new WorkingDataHistory());
    }

    private static void AssertOk(Result result) => Assert.True(result.IsSuccess, result.Error);

    private static Worksheet AssertWorksheet(Result<Worksheet> result)
    {
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

    private static string Snapshot(WorksheetWorkingData data) =>
        string.Join("|", data.Rows.Select(row => string.Join(",", row.Values.Select(v => v ?? "∅"))))
        + "#" + string.Join(",", data.Columns.Select(c => $"{c.SourceField}:{c.Header}"));

    private static DataRange Range() => new()
    {
        HeaderRowIndex = 1,
        DataStartRow = 2,
        DataEndRow = 3,
        StartColumn = 1,
        EndColumn = 2
    };

    private sealed class FakeReader : IExcelWorkbookReader
    {
        public Task<Result<Workbook>> OpenWorkbookAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<Workbook>("Not used"));

        public Task<Result<SheetPreview>> GetSheetPreviewAsync(string filePath, string worksheetName, int maxPreviewRows = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<SheetPreview>("Not used"));

        public Task<Result<DataRange>> DetectDataRangeAsync(string filePath, string worksheetName, int dataStartRow, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<DataRange>("Not used"));

        public Task<Result<WorksheetWorkingData>> ReadWorkingDataAsync(string filePath, string worksheetName, DataRange range, CancellationToken cancellationToken = default)
        {
            var data = new WorksheetWorkingData();
            data.Columns.Add(new WorkingDataColumn { SourceField = "A", Header = "Name", OriginalSourceColumn = "A" });
            data.Columns.Add(new WorkingDataColumn { SourceField = "B", Header = "Value", OriginalSourceColumn = "B" });
            data.Rows.Add(BuildRow(2, $"{worksheetName}-R1", "1"));
            data.Rows.Add(BuildRow(3, $"{worksheetName}-R2", "2"));
            return Task.FromResult(Result.Success(data));
        }

        private static WorkingDataRow BuildRow(int rowNumber, params string?[] values)
        {
            var row = new WorkingDataRow { OriginalRowNumber = rowNumber };
            foreach (var value in values) row.Values.Add(value);
            return row;
        }
    }
}
