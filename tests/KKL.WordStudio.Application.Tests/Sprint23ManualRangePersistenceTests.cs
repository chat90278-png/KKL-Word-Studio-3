namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;
using Xunit;

public sealed class Sprint23ManualRangePersistenceTests
{
    [Fact]
    public void SaveSelectedRange_CreatesWorksheetConfigurationWithoutCreatingWorkingData()
    {
        var project = new Project { Name = "Range project" };
        var service = new WorksheetWorkingDataService(new NeverReadingExcelReader());

        var worksheet = service.SaveSelectedRange(
            project,
            @"C:\Data\parts.xlsx",
            "parts.xlsx",
            "Sheet1",
            Range(header: 2, start: 3, end: 125, startColumn: 1, endColumn: 6));

        Assert.Single(project.DataSources);
        Assert.Equal("Sheet1", worksheet.Name);
        Assert.Null(worksheet.WorkingData);
        Assert.NotNull(worksheet.SelectedRange);
        Assert.Equal(2, worksheet.SelectedRange!.HeaderRowIndex);
        Assert.Equal(3, worksheet.SelectedRange.DataStartRow);
        Assert.Equal(125, worksheet.SelectedRange.DataEndRow);
        Assert.False(worksheet.SelectedRange.WasAutoDetected);
    }

    [Fact]
    public void SaveSelectedRange_KeepsIndependentRangesForWorksheetsInTheSameWorkbook()
    {
        var project = new Project();
        var service = new WorksheetWorkingDataService(new NeverReadingExcelReader());

        service.SaveSelectedRange(
            project,
            @"C:\Data\parts.xlsx",
            "parts.xlsx",
            "English",
            Range(header: 1, start: 2, end: 40, startColumn: 1, endColumn: 6));
        service.SaveSelectedRange(
            project,
            @"C:\Data\parts.xlsx",
            "parts.xlsx",
            "Turkish",
            Range(header: 4, start: 5, end: 90, startColumn: 2, endColumn: 7));

        var dataSource = Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources));
        Assert.Equal(2, dataSource.Workbook.Worksheets.Count);
        Assert.Equal(40, dataSource.Workbook.Worksheets.Single(w => w.Name == "English").SelectedRange!.DataEndRow);
        Assert.Equal(90, dataSource.Workbook.Worksheets.Single(w => w.Name == "Turkish").SelectedRange!.DataEndRow);
    }

    [Fact]
    public void SaveSelectedRange_UpdatesExistingWorksheetWithoutDiscardingWorkingData()
    {
        var project = new Project();
        var service = new WorksheetWorkingDataService(new NeverReadingExcelReader());
        var worksheet = service.SaveSelectedRange(
            project,
            @"C:\Data\parts.xlsx",
            "parts.xlsx",
            "Sheet1",
            Range(header: 1, start: 2, end: 20, startColumn: 1, endColumn: 6));
        var workingData = new WorksheetWorkingData();
        worksheet.WorkingData = workingData;

        var updated = service.SaveSelectedRange(
            project,
            @"C:\Data\parts.xlsx",
            "parts.xlsx",
            "Sheet1",
            Range(header: 2, start: 3, end: 200, startColumn: 1, endColumn: 6));

        Assert.Same(worksheet, updated);
        Assert.Same(workingData, updated.WorkingData);
        Assert.Equal(200, updated.SelectedRange!.DataEndRow);
        Assert.Single(Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources)).Workbook.Worksheets);
    }

    private static DataRange Range(
        int header,
        int start,
        int end,
        int startColumn,
        int endColumn) => new()
    {
        HeaderRowIndex = header,
        DataStartRow = start,
        DataEndRow = end,
        StartColumn = startColumn,
        EndColumn = endColumn,
        WasAutoDetected = false
    };

    private sealed class NeverReadingExcelReader : IExcelWorkbookReader
    {
        public Task<Result<Workbook>> OpenWorkbookAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<Workbook>("Not used by range persistence tests."));

        public Task<Result<SheetPreview>> GetSheetPreviewAsync(
            string filePath,
            string worksheetName,
            int maxPreviewRows = int.MaxValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<SheetPreview>("Not used by range persistence tests."));

        public Task<Result<WorksheetWorkingData>> ReadWorkingDataAsync(
            string filePath,
            string worksheetName,
            DataRange range,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<WorksheetWorkingData>("Not used by range persistence tests."));

        public Task<Result<DataRange>> DetectDataRangeAsync(
            string filePath,
            string worksheetName,
            int dataStartRow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<DataRange>("Not used by range persistence tests."));
    }
}
