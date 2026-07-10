namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Shared.Results;
using Xunit;

public class Sprint13ExcelInteractionAndAutoRangeTests
{
    private readonly ExcelDataRangeDetector _detector = new();

    [Fact]
    public void AutoRange_IgnoresLeadingBlankRows()
    {
        var preview = Preview(
            (1, "", "", ""),
            (2, "", "", ""),
            (3, "", "Ad", "Tutar"),
            (4, "", "Ali", "10"),
            (5, "", "Ayşe", "20"));

        var result = _detector.Detect(preview);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Value.HeaderRowIndex);
        Assert.Equal(4, result.Value.DataStartRow);
    }

    [Fact]
    public void AutoRange_DetectsHeaderAndDataStart()
    {
        var result = _detector.Detect(Preview(
            (5, "Kod", "Açıklama", "Miktar"),
            (6, "P-1", "Vida", "4"),
            (7, "P-2", "Somun", "8")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(5, result.Value.HeaderRowIndex);
        Assert.Equal(6, result.Value.DataStartRow);
        Assert.Equal(ExcelDataRangeConfidence.High, result.Value.Confidence);
    }

    [Fact]
    public void AutoRange_SupportsNoHeaderDataset()
    {
        var result = _detector.Detect(Preview(
            (2, "Alice", "Istanbul"),
            (3, "Bob", "Ankara"),
            (4, "Cara", "Izmir")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value.HeaderRowIndex);
        Assert.Equal(2, result.Value.DataStartRow);
        Assert.True(result.Value.RequiresReview); // all-text rows are honestly ambiguous, but still kept as the best no-header candidate
    }

    [Fact]
    public void AutoRange_DetectsNonAStartAndEndColumns()
    {
        var result = _detector.Detect(Preview(
            (3, "", "Kod", "Ad", "Tutar", ""),
            (4, "", "P-1", "Vida", "10", ""),
            (5, "", "P-2", "Somun", "20", "")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value.StartColumn);
        Assert.Equal(4, result.Value.EndColumn);
    }

    [Fact]
    public void AutoRange_LowConfidence_RequiresReviewStatus()
    {
        var result = _detector.Detect(Preview((8, "Not", "Tek satır")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Value.RequiresReview);
        Assert.Equal(ExcelDataRangeConfidence.Low, result.Value.Confidence);
    }

    [Fact]
    public void SheetLoad_AutoDetectsWhenNoPersistedRange()
    {
        var worksheet = new Worksheet { Name = "Sheet1" };

        Assert.Equal(ExcelRangeLoadAction.AutoDetect, ExcelRangeLoadPolicy.Decide(worksheet));
    }

    [Fact]
    public void PersistedRange_IsNotOverwrittenByAutoDetection()
    {
        var range = new DataRange { DataStartRow = 5, DataEndRow = 9, StartColumn = 2, EndColumn = 7 };
        var worksheet = new Worksheet { Name = "Sheet1", SelectedRange = range };

        Assert.Equal(ExcelRangeLoadAction.UsePersistedRange, ExcelRangeLoadPolicy.Decide(worksheet));
        Assert.Same(range, worksheet.SelectedRange);
    }

    [Fact]
    public void WorkingDataWorksheet_IsNotRedetectedFromSource()
    {
        var worksheet = new Worksheet { Name = "Sheet1", WorkingData = BuildWorkingData() };

        Assert.Equal(ExcelRangeLoadAction.UseWorkingData, ExcelRangeLoadPolicy.Decide(worksheet));
    }

    [Fact]
    public void Transfer_UsesConfiguredStartAndEndColumns()
    {
        var (project, report) = ProjectWithReport();
        var service = new ExcelReportTransferService();
        var request = new ExcelTransferRequest
        {
            WorkbookFilePath = "book.xlsx",
            WorkbookFileName = "book.xlsx",
            WorksheetName = "Sheet1",
            Range = new DataRange { HeaderRowIndex = 3, DataStartRow = 4, DataEndRow = 8, StartColumn = 2, EndColumn = 4 },
            HeaderTexts = new[] { "Kod", "Ad", "Tutar" }
        };

        var result = service.Transfer(project, report, request);

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Equal(new[] { "B", "C", "D" }, result.Table!.Columns.Select(column => column.SourceField));
        var worksheet = Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources)).Workbook.Worksheets.Single();
        Assert.Equal(2, worksheet.SelectedRange!.StartColumn);
        Assert.Equal(4, worksheet.SelectedRange.EndColumn);
    }

    [Fact]
    public async Task EnsureWorkingData_UsesConfiguredColumnBounds()
    {
        var reader = new CapturingReader();
        var service = new WorksheetWorkingDataService(reader);
        var range = new DataRange { DataStartRow = 4, DataEndRow = 7, StartColumn = 2, EndColumn = 6 };

        var result = await service.EnsureCreatedAsync(new Project(), "book.xlsx", "book.xlsx", "Sheet1", range);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(reader.CapturedRange);
        Assert.Equal(2, reader.CapturedRange!.StartColumn);
        Assert.Equal(6, reader.CapturedRange.EndColumn);
    }

    [Fact]
    public void HeaderTexts_AreAlignedToConfiguredColumnBounds()
    {
        var preview = Preview((3, "Ignore-A", "Kod", "Ad", "Tutar", "Ignore-E"));

        var texts = ExcelRangeProjection.GetRowTexts(preview, rowNumber: 3, startColumn: 2, endColumn: 4);

        Assert.Equal(new[] { "Kod", "Ad", "Tutar" }, texts);
    }

    [Fact]
    public void FilteredRowContextInsert_UsesUnderlyingWorkingRow()
    {
        var data = BuildWorkingData(("hidden", "0", null), ("match-one", "1", null), ("match-two", "2", null));
        var worksheet = new Worksheet { Name = "Sheet1", WorkingData = data };
        var view = new WorkingDataViewState();
        view.SetRowFilter(0, "match");
        var target = WorkingDataInteractionResolver.ResolveRowIndex(data, view, displayRowIndex: 0);
        var service = new WorksheetWorkingDataService(new CapturingReader());

        Assert.Equal(1, target);
        Assert.True(service.InsertRow(worksheet, target).IsSuccess);
        Assert.Equal("hidden", data.Rows[0].Values[0]);
        Assert.Null(data.Rows[1].Values[0]);
        Assert.Equal("match-one", data.Rows[2].Values[0]);
    }

    [Fact]
    public void FilteredRowContextDelete_UsesUnderlyingWorkingRows()
    {
        var data = BuildWorkingData(("hidden", "0", null), ("match-one", "1", null), ("match-two", "2", null));
        var worksheet = new Worksheet { Name = "Sheet1", WorkingData = data };
        var view = new WorkingDataViewState();
        view.SetRowFilter(0, "match");
        var targets = WorkingDataInteractionResolver.ResolveRowIndexes(data, view, new[] { 0, 1 });
        var service = new WorksheetWorkingDataService(new CapturingReader());

        Assert.Equal(new[] { 1, 2 }, targets);
        Assert.True(service.DeleteRows(worksheet, targets).IsSuccess);
        Assert.Single(data.Rows);
        Assert.Equal("hidden", data.Rows[0].Values[0]);
    }

    [Fact]
    public void HiddenColumnContextDelete_ResolvesStableWorkingColumn()
    {
        var data = BuildWorkingData();
        var view = new WorkingDataViewState();
        view.SetColumnHidden(data.Columns[1], true);

        Assert.Equal(new[] { 0, 2 }, view.GetVisibleColumnIndexes(data));
        Assert.Equal(2, WorkingDataInteractionResolver.ResolveColumnIndex(data, "C"));
        Assert.Equal(2, WorkingDataInteractionResolver.ResolveColumnIndex(data, data.Columns[2].Id.ToString("D")));
    }

    [Fact]
    public async Task ContextColumnDelete_PreservesReferencedTableGuard()
    {
        var project = new Project();
        var service = new WorksheetWorkingDataService(new CapturingReader());
        var worksheetResult = await service.EnsureCreatedAsync(project, "book.xlsx", "book.xlsx", "Sheet1", Range());
        Assert.True(worksheetResult.IsSuccess, worksheetResult.Error);
        var worksheet = worksheetResult.Value;
        var dataSource = Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources));
        var report = AddReport(project);
        var table = new TableElement
        {
            Name = "Kritik Tablo",
            Binding = new Binding { DataSourceName = dataSource.Name, WorksheetName = worksheet.Name }
        };
        table.Columns.Add(new TableColumn { Header = "Value", SourceField = "B" });
        report.Pages[0].Sections[0].Root.Children.Add(table);
        var index = WorkingDataInteractionResolver.ResolveColumnIndex(worksheet.WorkingData!, "B");

        var result = service.DeleteColumns(project, dataSource, worksheet, new[] { index });

        Assert.True(result.IsFailure);
        Assert.Contains("Kritik Tablo", result.Error);
        Assert.Contains("sütun silinemedi", result.Error);
    }

    [Fact]
    public void ContextMutation_IsOneUndoStep()
    {
        var worksheet = new Worksheet { Name = "Sheet1", WorkingData = BuildWorkingData(("a", "1", "x"), ("b", "2", "y")) };
        var service = new WorksheetWorkingDataService(new CapturingReader());
        var history = new WorkingDataHistory();
        var cells = new[] { new WorkingDataCell(0, 0), new WorkingDataCell(1, 0) };

        var mutation = service.Mutate(worksheet, history, () => service.ClearCells(worksheet, cells));
        Assert.True(mutation.IsSuccess, mutation.Error);
        Assert.True(history.CanUndo);
        Assert.Null(worksheet.WorkingData!.Rows[0].Values[0]);
        Assert.Null(worksheet.WorkingData.Rows[1].Values[0]);

        Assert.True(service.Undo(worksheet, history).IsSuccess);
        Assert.Equal("a", worksheet.WorkingData.Rows[0].Values[0]);
        Assert.Equal("b", worksheet.WorkingData.Rows[1].Values[0]);
        Assert.False(history.CanUndo);
    }

    private static SheetPreview Preview(params object[] rowDefinitions)
    {
        var rows = rowDefinitions
            .Select(definition => (System.Runtime.CompilerServices.ITuple)definition)
            .Select(tuple => new
            {
                RowNumber = Convert.ToInt32(tuple[0]),
                Values = Enumerable.Range(1, tuple.Length - 1).Select(index => tuple[index]?.ToString() ?? string.Empty).ToList()
            })
            .ToList();
        return new SheetPreview
        {
            WorksheetName = "Sheet1",
            RowNumbers = rows.Select(row => row.RowNumber).ToList(),
            Rows = rows.Select(row => (IReadOnlyList<string>)row.Values).ToList(),
            ColumnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Values.Count),
            IsTruncated = false
        };
    }

    private static (Project Project, Report Report) ProjectWithReport()
    {
        var project = new Project { Name = "Project" };
        return (project, AddReport(project));
    }

    private static Report AddReport(Project project)
    {
        var report = new Report { Name = "Report" };
        var page = new Page();
        page.Sections.Add(new Section { Name = "Body", Kind = SectionKind.Body });
        report.Pages.Add(page);
        project.Reports.Add(report);
        return report;
    }

    private static DataRange Range() => new()
    {
        HeaderRowIndex = 1,
        DataStartRow = 2,
        DataEndRow = 3,
        StartColumn = 1,
        EndColumn = 3
    };

    private static WorksheetWorkingData BuildWorkingData(params (string? A, string? B, string? C)[] rows)
    {
        var data = new WorksheetWorkingData();
        data.Columns.Add(new WorkingDataColumn { SourceField = "A", Header = "Name", OriginalSourceColumn = "A" });
        data.Columns.Add(new WorkingDataColumn { SourceField = "B", Header = "Value", OriginalSourceColumn = "B" });
        data.Columns.Add(new WorkingDataColumn { SourceField = "C", Header = "Note", OriginalSourceColumn = "C" });
        foreach (var values in rows)
        {
            var row = new WorkingDataRow();
            row.Values.Add(values.A);
            row.Values.Add(values.B);
            row.Values.Add(values.C);
            data.Rows.Add(row);
        }
        return data;
    }

    private sealed class CapturingReader : IExcelWorkbookReader
    {
        public DataRange? CapturedRange { get; private set; }

        public Task<Result<Workbook>> OpenWorkbookAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<Workbook>("Not used"));

        public Task<Result<SheetPreview>> GetSheetPreviewAsync(string filePath, string worksheetName, int maxPreviewRows = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<SheetPreview>("Not used"));

        public Task<Result<DataRange>> DetectDataRangeAsync(string filePath, string worksheetName, int dataStartRow, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<DataRange>("Not used"));

        public Task<Result<WorksheetWorkingData>> ReadWorkingDataAsync(string filePath, string worksheetName, DataRange range, CancellationToken cancellationToken = default)
        {
            CapturedRange = new DataRange
            {
                DataStartRow = range.DataStartRow,
                DataEndRow = range.DataEndRow,
                HeaderRowIndex = range.HeaderRowIndex,
                StartColumn = range.StartColumn,
                EndColumn = range.EndColumn,
                WasAutoDetected = range.WasAutoDetected
            };
            return Task.FromResult(Result.Success(BuildWorkingData(("r1", "1", "x"), ("r2", "2", "y"))));
        }
    }
}
