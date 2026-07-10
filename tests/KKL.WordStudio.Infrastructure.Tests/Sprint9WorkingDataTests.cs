namespace KKL.WordStudio.Infrastructure.Tests;

using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.WorkingData;
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
using DomainPage = KKL.WordStudio.Domain.Reports.Page;
using DomainWorkbook = KKL.WordStudio.Domain.DataSources.Workbook;
using DomainWorksheet = KKL.WordStudio.Domain.DataSources.Worksheet;
using DomainDataField = KKL.WordStudio.Domain.DataBinding.DataField;
using DomainTableColumn = KKL.WordStudio.Domain.Elements.TableColumn;

public class Sprint9WorkingDataTests
{
    [Fact]
    public async Task WorkingData_IsCreatedWithoutModifyingOriginalWorkbook()
    {
        var workbookPath = CreateWorkbook();
        try
        {
            var before = HashFile(workbookPath);
            var (project, service, worksheet) = await CreateWorkingDataAsync(workbookPath);

            Assert.True(service.SetCell(worksheet, 0, 0, "Edited").IsSuccess);

            Assert.NotNull(worksheet.WorkingData);
            Assert.Equal(before, HashFile(workbookPath));
            Assert.Single(project.DataSources);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task WorkingData_CellEdit_IsUsedByExcelDataProvider()
    {
        var workbookPath = CreateWorkbook();
        try
        {
            var (project, service, worksheet) = await CreateWorkingDataAsync(workbookPath);
            Assert.True(service.SetCell(worksheet, 0, 0, "Edited-Value").IsSuccess);
            var dataSource = Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources));

            var provider = new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance);
            var result = await provider.GetRowsAsync(dataSource, worksheetNameOverride: "Sheet1");

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal("Edited-Value", result.Value[0]["A"]);
            Assert.Equal("Original-2", result.Value[1]["A"]);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task WorkingData_RoundTripsThroughProjectPersistence()
    {
        var workbookPath = CreateWorkbook();
        var projectPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".kws");
        try
        {
            var (project, service, worksheet) = await CreateWorkingDataAsync(workbookPath);
            Assert.True(service.SetCell(worksheet, 0, 0, "Persisted-Edit").IsSuccess);
            Assert.True(service.InsertColumn(worksheet, 1).IsSuccess);
            Assert.True(service.SetCell(worksheet, 0, 1, "Project-Only").IsSuccess);
            var insertedField = worksheet.WorkingData!.Columns[1].SourceField;

            var repository = new KwsProjectRepository(NullLogger<KwsProjectRepository>.Instance);
            Assert.True((await repository.SaveAsync(project, projectPath)).IsSuccess);
            var opened = await repository.OpenAsync(projectPath);

            Assert.True(opened.IsSuccess, opened.Error);
            var roundTrippedWorksheet = Assert.IsType<ExcelDataSource>(Assert.Single(opened.Value.DataSources)).Workbook.Worksheets.Single();
            Assert.Equal("Persisted-Edit", roundTrippedWorksheet.WorkingData!.Rows[0].Values[0]);
            Assert.Equal("Project-Only", roundTrippedWorksheet.WorkingData.Rows[0].Values[1]);
            Assert.Equal(insertedField, roundTrippedWorksheet.WorkingData.Columns[1].SourceField);
        }
        finally
        {
            File.Delete(workbookPath);
            File.Delete(projectPath);
        }
    }

    [Fact]
    public async Task WorkingData_Reset_FallsBackToOriginalWorkbook()
    {
        var workbookPath = CreateWorkbook();
        try
        {
            var (project, service, worksheet) = await CreateWorkingDataAsync(workbookPath);
            var dataSource = Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources));
            var provider = new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance);
            Assert.True(service.SetCell(worksheet, 0, 0, "Edited").IsSuccess);
            Assert.Equal("Edited", (await provider.GetRowsAsync(dataSource, worksheetNameOverride: "Sheet1")).Value[0]["A"]);

            service.Reset(worksheet);
            var originalRows = await provider.GetRowsAsync(dataSource, worksheetNameOverride: "Sheet1");

            Assert.True(originalRows.IsSuccess, originalRows.Error);
            Assert.Equal("Original-1", originalRows.Value[0]["A"]);
            Assert.Null(worksheet.WorkingData);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task WorkingData_MissingSource_RemainsUsable()
    {
        var workbookPath = CreateWorkbook();
        var (project, service, worksheet) = await CreateWorkingDataAsync(workbookPath);
        Assert.True(service.SetCell(worksheet, 0, 0, "Offline-Edit").IsSuccess);
        var dataSource = Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources));
        File.Delete(workbookPath);

        var provider = new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance);
        var result = await provider.GetRowsAsync(dataSource, worksheetNameOverride: "Sheet1");

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("Offline-Edit", result.Value[0]["A"]);
    }

    [Fact]
    public async Task EditedWorkingData_IsReflectedInReportContentDocument()
    {
        var workbookPath = CreateWorkbook();
        try
        {
            var (project, service, worksheet) = await CreateWorkingDataAsync(workbookPath);
            Assert.True(service.SetCell(worksheet, 0, 0, "Preview-And-Word-Edit").IsSuccess);
            worksheet.ColumnMappings.Add(new ColumnMapping
            {
                SourceColumn = "A",
                TargetField = new DomainDataField { Name = "PartName", DataType = "Text" }
            });
            var dataSource = Assert.IsType<ExcelDataSource>(Assert.Single(project.DataSources));
            var report = CreateBoundReport(dataSource.Name, "PartName");
            project.Reports.Add(report);

            var registry = new DataProviderRegistry();
            registry.Register(new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance));
            var builder = new ReportContentBuilder(registry);
            var content = await builder.BuildAsync(project, report);
            var tableNode = Assert.IsType<TableContentNode>(Assert.Single(content.BodyNodes));
            Assert.Equal("Preview-And-Word-Edit", tableNode.Rows[0][0]);

            var exporter = new WordExporter(builder, NullLogger<WordExporter>.Instance);
            var export = await exporter.ExportAsync(project, report, ExportOptions.Default);
            Assert.True(export.IsSuccess, export.Error);
            using var wordDocument = WordprocessingDocument.Open(export.Value, false);
            var wordText = wordDocument.MainDocumentPart!.Document.Body!.InnerText;
            Assert.Contains("Preview-And-Word-Edit", wordText);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    private static async Task<(Project Project, WorksheetWorkingDataService Service, DomainWorksheet Worksheet)> CreateWorkingDataAsync(string workbookPath)
    {
        var reader = new OpenXmlExcelWorkbookReader(NullLogger<OpenXmlExcelWorkbookReader>.Instance);
        var service = new WorksheetWorkingDataService(reader);
        var project = new Project { Name = "Working Data Project" };
        var result = await service.EnsureCreatedAsync(
            project,
            workbookPath,
            Path.GetFileName(workbookPath),
            "Sheet1",
            new DataRange
            {
                HeaderRowIndex = 1,
                DataStartRow = 2,
                DataEndRow = 3,
                StartColumn = 1,
                EndColumn = 2
            });
        Assert.True(result.IsSuccess, result.Error);
        return (project, service, result.Value);
    }

    private static Report CreateBoundReport(string dataSourceName, string firstSourceField = "A")
    {
        var report = new Report { Name = "Report" };
        var page = new DomainPage();
        var section = new Section { Kind = SectionKind.Body };
        var table = new TableElement
        {
            Name = "Table",
            Binding = new Binding { DataSourceName = dataSourceName, WorksheetName = "Sheet1" }
        };
        table.Columns.Add(new DomainTableColumn { Header = "Name", SourceField = firstSourceField });
        table.Columns.Add(new DomainTableColumn { Header = "Value", SourceField = "B" });
        section.Root.Children.Add(table);
        page.Sections.Add(section);
        report.Pages.Add(page);
        return report;
    }

    private static string CreateWorkbook()
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

        sheetData.Append(BuildRow(1, ("A1", "Name"), ("B1", "Value")));
        sheetData.Append(BuildRow(2, ("A2", "Original-1"), ("B2", "1")));
        sheetData.Append(BuildRow(3, ("A3", "Original-2"), ("B3", "2")));
        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();
        return filePath;
    }

    private static Row BuildRow(uint rowIndex, params (string Reference, string Value)[] cells)
    {
        var row = new Row { RowIndex = rowIndex };
        foreach (var (reference, value) in cells)
        {
            row.Append(new Cell
            {
                CellReference = reference,
                DataType = CellValues.String,
                CellValue = new CellValue(value)
            });
        }
        return row;
    }

    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
