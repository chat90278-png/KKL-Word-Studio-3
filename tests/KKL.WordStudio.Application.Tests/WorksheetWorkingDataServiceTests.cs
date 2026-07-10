namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;
using Xunit;

public class WorksheetWorkingDataServiceTests
{
    [Fact]
    public async Task WorkingData_RowInsertDelete_PreservesOrder()
    {
        var service = new WorksheetWorkingDataService(new FakeExcelReader());
        var project = new Project();
        var worksheet = AssertSuccess(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet1", Range()));

        Assert.Equal(new[] { "Sheet1-R1", "Sheet1-R2" }, worksheet.WorkingData!.Rows.Select(row => row.Values[0]));

        Assert.True(service.InsertRow(worksheet, 1).IsSuccess);
        Assert.True(service.SetCell(worksheet, 1, 0, "Inserted").IsSuccess);
        Assert.True(service.DeleteRows(worksheet, new[] { 0 }).IsSuccess);

        Assert.Equal(new[] { "Inserted", "Sheet1-R2" }, worksheet.WorkingData.Rows.Select(row => row.Values[0]));
    }

    [Fact]
    public async Task WorkingData_ColumnInsertDelete_PreservesStableFieldIdentity()
    {
        var service = new WorksheetWorkingDataService(new FakeExcelReader());
        var project = new Project();
        var worksheet = AssertSuccess(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet1", Range()));
        var originalB = worksheet.WorkingData!.Columns[1];

        Assert.True(service.InsertColumn(worksheet, 1).IsSuccess);
        Assert.Equal(originalB.Id, worksheet.WorkingData.Columns[2].Id);
        Assert.Equal("B", worksheet.WorkingData.Columns[2].SourceField);

        var dataSource = Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources));
        Assert.True(service.DeleteColumns(project, dataSource, worksheet, new[] { 1 }).IsSuccess);
        Assert.Equal(originalB.Id, worksheet.WorkingData.Columns[1].Id);
        Assert.Equal("B", worksheet.WorkingData.Columns[1].SourceField);
    }

    [Fact]
    public async Task WorkingData_IsIsolatedPerWorksheet()
    {
        var service = new WorksheetWorkingDataService(new FakeExcelReader());
        var project = new Project();
        var sheet1 = AssertSuccess(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet1", Range()));
        var sheet2 = AssertSuccess(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet2", Range()));

        Assert.True(service.SetCell(sheet1, 0, 0, "Only-Sheet1").IsSuccess);

        Assert.Equal("Only-Sheet1", sheet1.WorkingData!.Rows[0].Values[0]);
        Assert.Equal("Sheet2-R1", sheet2.WorkingData!.Rows[0].Values[0]);
        Assert.NotSame(sheet1.WorkingData, sheet2.WorkingData);
    }

    [Fact]
    public async Task ClipboardMatrixPaste_AppliesRectangularValues()
    {
        var service = new WorksheetWorkingDataService(new FakeExcelReader());
        var project = new Project();
        var worksheet = AssertSuccess(await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet1", Range()));

        var result = service.ApplyClipboardMatrix(worksheet, 0, 0, "A1\tB1\r\nA2\tB2\r\n");

        Assert.True(result.IsSuccess);
        Assert.Equal(new string?[] { "A1", "B1" }, worksheet.WorkingData!.Rows[0].Values);
        Assert.Equal(new string?[] { "A2", "B2" }, worksheet.WorkingData.Rows[1].Values);
    }

    private static Worksheet AssertSuccess(Result<Worksheet> result)
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

    private sealed class FakeExcelReader : IExcelWorkbookReader
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
