namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.DataSources;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.DataProviders;
using KKL.WordStudio.Infrastructure.Excel;
using KKL.WordStudio.Infrastructure.Export.Exporters;
using KKL.WordStudio.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainTableColumn = KKL.WordStudio.Domain.Elements.TableColumn;
using DomainWorksheet = KKL.WordStudio.Domain.DataSources.Worksheet;
using DomainPage = KKL.WordStudio.Domain.Reports.Page;
using DomainWorkbook = KKL.WordStudio.Domain.DataSources.Workbook;
using DomainDataField = KKL.WordStudio.Domain.DataBinding.DataField;

public class Sprint10MultiSourceTests
{
    [Fact]
    public async Task MultiSourceTable_AppendsRowsInConfiguredSourceOrder()
    {
        var project = new Project { Name = "Multi" };
        var source1 = AddWorkingSource(project, "Aircraft", "Aircraft.xlsx", "Sheet1", new[] { ("A", "Name") }, new[] { new[] { "A1" }, new[] { "A2" } });
        var source2 = AddWorkingSource(project, "Engine", "Engine.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "E1" } });
        var (report, table) = CreateComposedReport(("Ad", "Name"));
        AddTableSource(table, source1, "Sheet1", "A");
        AddTableSource(table, source2, "Data", "A");
        project.Reports.Add(report);

        var node = await BuildTableNodeAsync(project, report);

        Assert.Equal(new[] { "A1", "A2", "E1" }, node.Rows.Select(row => row[0]));
        Assert.Equal(2, node.SourceCount);
    }

    [Fact]
    public async Task MultiSourceTable_SupportsSameWorkbookDifferentWorksheets()
    {
        var project = new Project { Name = "Same workbook" };
        var dataSource = new ExcelDataSource
        {
            Name = "Aircraft",
            Workbook = new DomainWorkbook { FileName = "Aircraft.xlsx", SourcePath = MissingPath("Aircraft.xlsx") },
            ActiveWorksheetName = "Sheet1"
        };
        dataSource.Workbook.Worksheets.Add(CreateWorkingWorksheet("Sheet1", new[] { ("A", "Name") }, new[] { new[] { "S1" } }));
        dataSource.Workbook.Worksheets.Add(CreateWorkingWorksheet("Sheet2", new[] { ("A", "Name") }, new[] { new[] { "S2" } }));
        project.DataSources.Add(dataSource);
        var (report, table) = CreateComposedReport(("Ad", "Name"));
        AddTableSource(table, dataSource, "Sheet1", "A");
        AddTableSource(table, dataSource, "Sheet2", "A");
        project.Reports.Add(report);

        var node = await BuildTableNodeAsync(project, report);

        Assert.Equal(new[] { "S1", "S2" }, node.Rows.Select(row => row[0]));
    }

    [Fact]
    public async Task MultiSourceTable_SupportsDifferentWorkbooks()
    {
        var project = new Project { Name = "Different workbooks" };
        var source1 = AddWorkingSource(project, "One", "One.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "One" } });
        var source2 = AddWorkingSource(project, "Two", "Two.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "Two" } });
        var (report, table) = CreateComposedReport(("Ad", "Name"));
        AddTableSource(table, source1, "Data", "A");
        AddTableSource(table, source2, "Data", "A");
        project.Reports.Add(report);

        var node = await BuildTableNodeAsync(project, report);

        Assert.Equal(new[] { "One", "Two" }, node.Rows.Select(row => row[0]));
    }

    [Fact]
    public async Task PerSourceFieldMapping_NormalizesDifferentSchemas()
    {
        var project = new Project { Name = "Schemas" };
        var parts = AddWorkingSource(project, "Parts", "Parts.xlsx", "Data",
            new[] { ("PartName", "Part"), ("PartQty", "Qty") },
            new[] { new[] { "Wing", "2" } });
        var engines = AddWorkingSource(project, "Engines", "Engines.xlsx", "Data",
            new[] { ("EngineLabel", "Engine"), ("EngineCount", "Count") },
            new[] { new[] { "Turbofan", "4" } });
        var (report, table) = CreateComposedReport(("Ad", "LogicalName"), ("Adet", "LogicalCount"));
        AddTableSource(table, parts, "Data", "PartName", "PartQty");
        AddTableSource(table, engines, "Data", "EngineLabel", "EngineCount");
        project.Reports.Add(report);

        var node = await BuildTableNodeAsync(project, report);

        Assert.Equal(new[] { "Ad", "Adet" }, node.ColumnHeaders);
        Assert.Equal(new[] { "Wing", "2" }, node.Rows[0]);
        Assert.Equal(new[] { "Turbofan", "4" }, node.Rows[1]);
    }

    [Fact]
    public async Task SourceOrder_RoundTripsThroughProjectPersistence()
    {
        var project = new Project { Name = "Persist order" };
        var source1 = AddWorkingSource(project, "First", "First.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "1" } });
        var source2 = AddWorkingSource(project, "Second", "Second.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "2" } });
        var (report, table) = CreateComposedReport(("Ad", "Name"));
        AddTableSource(table, source2, "Data", "A");
        AddTableSource(table, source1, "Data", "A");
        project.Reports.Add(report);
        var projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".kws");

        try
        {
            var repository = new KwsProjectRepository(NullLogger<KwsProjectRepository>.Instance);
            Assert.True((await repository.SaveAsync(project, projectPath)).IsSuccess);
            var opened = await repository.OpenAsync(projectPath);

            Assert.True(opened.IsSuccess, opened.Error);
            var openedTable = FindOnlyTable(opened.Value.Reports.Single());
            Assert.Equal(new[] { "Second", "First" }, openedTable.Sources.Select(source => source.DataSourceName));
        }
        finally
        {
            System.IO.File.Delete(projectPath);
        }
    }

    [Fact]
    public async Task ReorderingSources_ChangesComposedRowOrder()
    {
        var project = new Project { Name = "Reorder" };
        var source1 = AddWorkingSource(project, "First", "First.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "First" } });
        var source2 = AddWorkingSource(project, "Second", "Second.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "Second" } });
        var (report, table) = CreateComposedReport(("Ad", "Name"));
        AddTableSource(table, source1, "Data", "A");
        AddTableSource(table, source2, "Data", "A");
        project.Reports.Add(report);
        var service = new TableSourceCompositionService();

        Assert.True(service.MoveSource(table, 1, -1).IsSuccess);
        var node = await BuildTableNodeAsync(project, report);

        Assert.Equal(new[] { "Second", "First" }, node.Rows.Select(row => row[0]));
    }

    [Fact]
    public async Task LegacySingleBinding_StillRenders()
    {
        var project = new Project { Name = "Legacy" };
        var source = AddWorkingSource(project, "LegacySource", "Legacy.xlsx", "Sheet1", new[] { ("A", "Name") }, new[] { new[] { "Legacy row" } });
        var (report, table) = CreateComposedReport(("Ad", "A"));
        table.Binding = new Binding { DataSourceName = source.Name, WorksheetName = "Sheet1" };
        project.Reports.Add(report);

        var node = await BuildTableNodeAsync(project, report);

        Assert.Empty(table.Sources);
        Assert.Equal("Legacy row", Assert.Single(node.Rows)[0]);
        Assert.Equal(1, node.SourceCount);
    }

    [Fact]
    public async Task WorkingData_HasPrecedenceInsideMultiSourceComposition()
    {
        var workbookPath = CreateWorkbook("Original");
        try
        {
            var project = new Project { Name = "Working precedence" };
            var dataSource = new ExcelDataSource
            {
                Name = "Excel",
                Workbook = new DomainWorkbook { FileName = Path.GetFileName(workbookPath), SourcePath = workbookPath },
                ActiveWorksheetName = "Sheet1"
            };
            var worksheet = CreateWorkingWorksheet("Sheet1", new[] { ("A", "Name") }, new[] { new[] { "Edited" } });
            dataSource.Workbook.Worksheets.Add(worksheet);
            project.DataSources.Add(dataSource);
            var (report, table) = CreateComposedReport(("Ad", "Name"));
            AddTableSource(table, dataSource, "Sheet1", "A");
            project.Reports.Add(report);

            var node = await BuildTableNodeAsync(project, report);

            Assert.Equal("Edited", Assert.Single(node.Rows)[0]);
        }
        finally
        {
            System.IO.File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task MissingMultiSourceInput_IsSurfacedWithoutCrashing()
    {
        var project = new Project { Name = "Missing" };
        var dataSource = new ExcelDataSource
        {
            Name = "MissingSource",
            Workbook = new DomainWorkbook { FileName = "Missing.xlsx", SourcePath = MissingPath("Missing.xlsx") },
            ActiveWorksheetName = "Data"
        };
        dataSource.Workbook.Worksheets.Add(new DomainWorksheet
        {
            Name = "Data",
            SelectedRange = CreateRange(2, 3, 1)
        });
        project.DataSources.Add(dataSource);
        var (report, table) = CreateComposedReport(("Ad", "Name"));
        AddTableSource(table, dataSource, "Data", "A");
        project.Reports.Add(report);
        var builder = CreateBuilder();

        var content = await builder.BuildAsync(project, report);
        var node = Assert.IsType<TableContentNode>(Assert.Single(content.BodyNodes));
        var exporter = new WordExporter(builder, NullLogger<WordExporter>.Instance);
        var export = await exporter.ExportAsync(project, report, ExportOptions.Default);

        Assert.NotNull(node.SourceError);
        Assert.Contains("MissingSource / Data", node.SourceError!);
        Assert.True(export.IsFailure);
        Assert.Contains("MissingSource / Data", export.Error!);
    }

    [Fact]
    public async Task MultiSourceRows_AreIdenticalInReportContentAndWord()
    {
        var project = new Project { Name = "Consistency" };
        var source1 = AddWorkingSource(project, "First", "First.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "FirstValue" } });
        var source2 = AddWorkingSource(project, "Second", "Second.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "SecondValue" } });
        var (report, table) = CreateComposedReport(("Ad", "Name"));
        AddTableSource(table, source1, "Data", "A");
        AddTableSource(table, source2, "Data", "A");
        project.Reports.Add(report);
        var builder = CreateBuilder();

        var content = await builder.BuildAsync(project, report);
        var node = Assert.IsType<TableContentNode>(Assert.Single(content.BodyNodes));
        var semanticValues = node.Rows.Select(row => row[0]).ToList();
        var exporter = new WordExporter(builder, NullLogger<WordExporter>.Instance);
        var export = await exporter.ExportAsync(project, report, ExportOptions.Default);

        Assert.True(export.IsSuccess, export.Error);
        using var wordDocument = WordprocessingDocument.Open(export.Value, false);
        var wordText = wordDocument.MainDocumentPart!.Document.Body!.InnerText;
        Assert.Equal(new[] { "FirstValue", "SecondValue" }, semanticValues);
        var firstIndex = wordText.IndexOf(semanticValues[0], StringComparison.Ordinal);
        var secondIndex = wordText.IndexOf(semanticValues[1], StringComparison.Ordinal);
        Assert.True(firstIndex >= 0, "First composed value was not written to Word.");
        Assert.True(secondIndex > firstIndex, "Word row order did not match ReportContentDocument row order.");
    }

    [Fact]
    public void AddingSource_DoesNotOverwriteExistingDisplayHeaders()
    {
        var project = new Project { Name = "Transfer" };
        var existingSource = new ExcelDataSource
        {
            Name = "Existing",
            Workbook = new DomainWorkbook { FileName = "Existing.xlsx", SourcePath = "/tmp/Existing.xlsx" },
            ActiveWorksheetName = "Sheet1"
        };
        var existingWorksheet = new DomainWorksheet { Name = "Sheet1", SelectedRange = CreateRange(2, 3, 1) };
        existingWorksheet.ColumnMappings.Add(new ColumnMapping
        {
            SourceColumn = "A",
            TargetField = new DomainDataField { Name = "Name", DataType = "Text" }
        });
        existingSource.Workbook.Worksheets.Add(existingWorksheet);
        project.DataSources.Add(existingSource);
        var (report, table) = CreateComposedReport(("Özel Görünen Başlık", "Name"));
        table.Binding = new Binding { DataSourceName = "Existing", WorksheetName = "Sheet1" };
        project.Reports.Add(report);
        var request = new ExcelTransferRequest
        {
            WorkbookFilePath = "/tmp/New.xlsx",
            WorkbookFileName = "New.xlsx",
            WorksheetName = "Data",
            Range = CreateRange(2, 3, 1),
            HeaderTexts = new[] { "Başka Başlık" },
            AppliedColumnMappings = new[] { new TransferColumnMapping { SourceColumn = "A", FieldName = "Name", DataType = "Text" } },
            TargetElementId = table.Id,
            ExistingTableMode = ExistingTableTransferMode.AddAsSource
        };

        var result = new ExcelReportTransferService().Transfer(project, report, request);

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.True(result.AddedAsSource);
        Assert.Equal("Özel Görünen Başlık", table.Columns[0].Header);
        Assert.Equal(2, table.Sources.Count);
        Assert.Equal(new[] { "Existing", "New" }, table.Sources.Select(source => source.DataSourceName));
    }

    [Fact]
    public void RemovingSource_DoesNotDeleteProjectDataSource()
    {
        var project = new Project { Name = "Remove" };
        var source1 = AddWorkingSource(project, "First", "First.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "1" } });
        var source2 = AddWorkingSource(project, "Second", "Second.xlsx", "Data", new[] { ("A", "Name") }, new[] { new[] { "2" } });
        var (_, table) = CreateComposedReport(("Ad", "Name"));
        AddTableSource(table, source1, "Data", "A");
        AddTableSource(table, source2, "Data", "A");
        var service = new TableSourceCompositionService();

        var result = service.RemoveSource(table, 0);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, project.DataSources.Count);
        Assert.Single(table.Sources);
        Assert.Equal("Second", table.Sources[0].DataSourceName);
    }

    [Fact]
    public async Task MultiSourceOriginalExcel_UsesPinnedRangeOverride()
    {
        var workbookPath = CreateWorkbook("Row2", "Row3", "Row4");
        try
        {
            var project = new Project { Name = "Pinned range" };
            var source = new ExcelDataSource
            {
                Name = "Excel",
                Workbook = new DomainWorkbook { FileName = Path.GetFileName(workbookPath), SourcePath = workbookPath },
                ActiveWorksheetName = "Sheet1"
            };
            source.Workbook.Worksheets.Add(new DomainWorksheet
            {
                Name = "Sheet1",
                SelectedRange = CreateRange(2, 4, 1)
            });
            project.DataSources.Add(source);
            var (report, table) = CreateComposedReport(("Ad", "Name"));
            var pinned = new TableSourceBinding
            {
                DataSourceName = source.Name,
                WorksheetName = "Sheet1",
                Range = CreateRange(3, 3, 1)
            };
            pinned.FieldMappings.Add(new TableSourceFieldMapping { TableColumnId = table.Columns[0].Id, SourceField = "A" });
            table.Sources.Add(pinned);
            project.Reports.Add(report);

            var node = await BuildTableNodeAsync(project, report);

            Assert.Equal("Row3", Assert.Single(node.Rows)[0]);
        }
        finally
        {
            System.IO.File.Delete(workbookPath);
        }
    }

    private static ReportContentBuilder CreateBuilder()
    {
        var registry = new DataProviderRegistry();
        registry.Register(new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance));
        return new ReportContentBuilder(registry);
    }

    private static async Task<TableContentNode> BuildTableNodeAsync(Project project, Report report)
    {
        var content = await CreateBuilder().BuildAsync(project, report);
        return Assert.IsType<TableContentNode>(Assert.Single(content.BodyNodes));
    }

    private static (Report Report, TableElement Table) CreateComposedReport(params (string Header, string SourceField)[] columns)
    {
        var report = new Report { Name = "Report" };
        var page = new DomainPage();
        var section = new Section { Kind = SectionKind.Body };
        var table = new TableElement { Name = "Table" };
        foreach (var (header, sourceField) in columns)
            table.Columns.Add(new DomainTableColumn { Header = header, SourceField = sourceField });
        section.Root.Children.Add(table);
        page.Sections.Add(section);
        report.Pages.Add(page);
        return (report, table);
    }

    private static ExcelDataSource AddWorkingSource(
        Project project,
        string sourceName,
        string workbookName,
        string worksheetName,
        IReadOnlyList<(string Field, string Header)> columns,
        IReadOnlyList<string[]> rows)
    {
        var source = new ExcelDataSource
        {
            Name = sourceName,
            Workbook = new DomainWorkbook { FileName = workbookName, SourcePath = MissingPath(workbookName) },
            ActiveWorksheetName = worksheetName
        };
        source.Workbook.Worksheets.Add(CreateWorkingWorksheet(worksheetName, columns, rows));
        project.DataSources.Add(source);
        return source;
    }

    private static DomainWorksheet CreateWorkingWorksheet(
        string worksheetName,
        IReadOnlyList<(string Field, string Header)> columns,
        IReadOnlyList<string[]> rows)
    {
        var worksheet = new DomainWorksheet
        {
            Name = worksheetName,
            SelectedRange = CreateRange(2, rows.Count + 1, columns.Count),
            WorkingData = new WorksheetWorkingData()
        };
        foreach (var (field, header) in columns)
            worksheet.WorkingData.Columns.Add(new WorkingDataColumn { SourceField = field, Header = header });
        foreach (var values in rows)
        {
            var row = new WorkingDataRow();
            foreach (var value in values) row.Values.Add(value);
            worksheet.WorkingData.Rows.Add(row);
        }
        return worksheet;
    }

    private static void AddTableSource(TableElement table, ExcelDataSource source, string worksheetName, params string[] providerFields)
    {
        var worksheet = source.Workbook.Worksheets.Single(candidate => candidate.Name == worksheetName);
        var tableSource = new TableSourceBinding
        {
            DataSourceName = source.Name,
            WorksheetName = worksheetName,
            Range = CloneRange(worksheet.SelectedRange!)
        };
        for (var index = 0; index < table.Columns.Count; index++)
        {
            tableSource.FieldMappings.Add(new TableSourceFieldMapping
            {
                TableColumnId = table.Columns[index].Id,
                SourceField = providerFields[index]
            });
        }
        table.Sources.Add(tableSource);
    }

    private static TableElement FindOnlyTable(Report report) =>
        Assert.IsType<TableElement>(Assert.Single(report.Pages.Single().Sections.Single().Root.Children));

    private static DataRange CreateRange(int startRow, int endRow, int columnCount) => new()
    {
        HeaderRowIndex = 1,
        DataStartRow = startRow,
        DataEndRow = endRow,
        StartColumn = 1,
        EndColumn = columnCount
    };

    private static DataRange CloneRange(DataRange range) => new()
    {
        HeaderRowIndex = range.HeaderRowIndex,
        DataStartRow = range.DataStartRow,
        DataEndRow = range.DataEndRow,
        StartColumn = range.StartColumn,
        EndColumn = range.EndColumn,
        WasAutoDetected = range.WasAutoDetected
    };

    private static string CreateWorkbook(params string[] values)
    {
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xlsx");
        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet(sheetData);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Sheet1" });
        sheetData.Append(BuildRow(1, "Name"));
        for (var index = 0; index < values.Length; index++)
            sheetData.Append(BuildRow((uint)index + 2, values[index]));
        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();
        return filePath;
    }

    private static Row BuildRow(uint rowIndex, string value)
    {
        var row = new Row { RowIndex = rowIndex };
        row.Append(new Cell
        {
            CellReference = $"A{rowIndex}",
            DataType = CellValues.String,
            CellValue = new CellValue(value)
        });
        return row;
    }
    private static string MissingPath(string fileName)
        => Path.Combine(Path.GetTempPath(), "KKL-WordStudio-Missing", Guid.NewGuid().ToString("N"), fileName);

}
