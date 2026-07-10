namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;
using Xunit;

public class Sprint11HistoryRegistryTests
{
    [Fact]
    public async Task ResetWorkingData_ClearsWorksheetUndoHistory()
    {
        var service = new WorksheetWorkingDataService(new FakeReader());
        var registry = new WorkingDataHistoryRegistry();
        var project = new Project();
        var worksheet = AssertWorksheet(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet1", Range()));

        AssertOk(service.Mutate(worksheet, registry.For(worksheet), () => service.SetCell(worksheet, 0, 0, "x")));
        Assert.True(registry.For(worksheet).CanUndo);

        // Reset-to-source drops working data and must clear that worksheet's history.
        service.Reset(worksheet);
        registry.Forget(worksheet);

        Assert.False(registry.For(worksheet).CanUndo);
        Assert.False(registry.For(worksheet).CanRedo);
    }

    [Fact]
    public void ProjectOpenOrNew_DoesNotResurrectStaleHistory()
    {
        var registry = new WorkingDataHistoryRegistry();
        var worksheet = new Worksheet { Name = "Sheet1" };
        var history = registry.For(worksheet);
        history.Record(EmptySnapshot());
        Assert.True(registry.For(worksheet).CanUndo);

        // Simulate project open/new: registry hard-clears runtime histories.
        registry.Clear();

        Assert.False(registry.For(worksheet).CanUndo);
    }

    [Fact]
    public void Registry_ReturnsSameHistoryInstancePerWorksheet()
    {
        var registry = new WorkingDataHistoryRegistry();
        var worksheet = new Worksheet { Name = "Sheet1" };

        Assert.Same(registry.For(worksheet), registry.For(worksheet));
    }

    private static WorkingDataSnapshot EmptySnapshot() =>
        WorkingDataSnapshot.Capture(new WorksheetWorkingData());

    private static void AssertOk(Result result) => Assert.True(result.IsSuccess, result.Error);

    private static Worksheet AssertWorksheet(Result<Worksheet> result)
    {
        Assert.True(result.IsSuccess, result.Error);
        return result.Value;
    }

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
