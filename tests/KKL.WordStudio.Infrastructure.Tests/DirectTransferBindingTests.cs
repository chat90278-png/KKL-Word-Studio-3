namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Editing;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.DataProviders;
using KKL.WordStudio.Infrastructure.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainWorkbook = KKL.WordStudio.Domain.DataSources.Workbook;
using DomainWorksheet = KKL.WordStudio.Domain.DataSources.Worksheet;
using DomainPage = KKL.WordStudio.Domain.Reports.Page;

/// <summary>
/// End-to-end (real .xlsx, real OpenXML reader, real ExcelDataProvider)
/// coverage for the two direct-transfer regression requirements that need
/// an actual workbook on disk rather than an in-memory fixture: Sprint 7
/// acceptance tests 3 and 4.
/// </summary>
public class DirectTransferBindingTests
{
    /// <summary>Required test 3: transferring from two different worksheets of the same workbook (one after another, selecting a fresh unbound table each time) produces two stable, independently-resolving bindings.</summary>
    [Fact]
    public async Task TransferFromTwoWorksheets_CreatesStableIndependentBindings()
    {
        var workbookPath = CreateTwoSheetWorkbook();
        try
        {
            var project = new Project { Name = "Aircraft Project" };
            var report = new Report { Name = "Report" };
            var page = new DomainPage();
            var section = new Section { Kind = SectionKind.Body };
            page.Sections.Add(section);
            report.Pages.Add(page);
            project.Reports.Add(report);

            var table1 = new TableElement { Name = "Tablo 1" };
            var table2 = new TableElement { Name = "Tablo 2" };
            section.Root.Children.Add(table1);
            section.Root.Children.Add(table2);

            var transferService = new ExcelReportTransferService();

            var resultSheet1 = transferService.Transfer(project, report, new ExcelTransferRequest
            {
                WorkbookFilePath = workbookPath,
                WorkbookFileName = "Aircraft.xlsx",
                WorksheetName = "Sheet1",
                Range = new DataRange { DataStartRow = 2, DataEndRow = 2, HeaderRowIndex = 1, StartColumn = 1, EndColumn = 2 },
                HeaderTexts = new[] { "Name", "Value" },
                TargetElementId = table1.Id
            });

            var resultSheet2 = transferService.Transfer(project, report, new ExcelTransferRequest
            {
                WorkbookFilePath = workbookPath,
                WorkbookFileName = "Aircraft.xlsx",
                WorksheetName = "Sheet2",
                Range = new DataRange { DataStartRow = 2, DataEndRow = 2, HeaderRowIndex = 1, StartColumn = 1, EndColumn = 2 },
                HeaderTexts = new[] { "Name", "Value" },
                TargetElementId = table2.Id
            });

            Assert.Equal(TransferOutcome.Success, resultSheet1.Outcome);
            Assert.Equal(TransferOutcome.Success, resultSheet2.Outcome);
            Assert.Single(project.DataSources); // same workbook -> same ExcelDataSource, reused

            var registry = new DataProviderRegistry();
            registry.Register(new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance));
            var builder = new ReportContentBuilder(registry);

            // Simulate the user actively browsing Sheet2 (changing the
            // DataSource's ActiveWorksheetName) while both bound tables are
            // rendered — each table must keep resolving its OWN pinned
            // worksheet regardless (ADR 0009), exactly as
            // TableWorksheetBindingStabilityTests already locks in for the
            // manual-binding path; this proves the direct-transfer path
            // produces the same stable end state.
            ((ExcelDataSource)project.DataSources[0]).ActiveWorksheetName = "Sheet2";
            var document = await builder.BuildAsync(project, report);

            var table1Node = Assert.IsType<TableContentNode>(document.BodyNodes[0]);
            var table2Node = Assert.IsType<TableContentNode>(document.BodyNodes[1]);

            Assert.Equal("FromSheet1", Assert.Single(table1Node.Rows)[0]);
            Assert.Equal("FromSheet2", Assert.Single(table2Node.Rows)[0]);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    /// <summary>Required test 4: renaming a table's DISPLAYED header (the inline design-surface edit) must not change which source column its data resolves from — verified end-to-end against a real bound table built from a real transfer.</summary>
    [Fact]
    public async Task RenamingDisplayedTableHeader_DoesNotChangeSourceDataResolution_EndToEnd()
    {
        var workbookPath = CreateTwoSheetWorkbook();
        try
        {
            var project = new Project { Name = "Aircraft Project" };
            var report = new Report { Name = "Report" };
            var page = new DomainPage();
            var section = new Section { Kind = SectionKind.Body };
            page.Sections.Add(section);
            report.Pages.Add(page);
            project.Reports.Add(report);

            var table = new TableElement { Name = "Tablo 1" };
            section.Root.Children.Add(table);

            var transferService = new ExcelReportTransferService();
            var transferResult = transferService.Transfer(project, report, new ExcelTransferRequest
            {
                WorkbookFilePath = workbookPath,
                WorkbookFileName = "Aircraft.xlsx",
                WorksheetName = "Sheet1",
                Range = new DataRange { DataStartRow = 2, DataEndRow = 2, HeaderRowIndex = 1, StartColumn = 1, EndColumn = 2 },
                HeaderTexts = new[] { "Name", "Value" },
                TargetElementId = table.Id
            });
            Assert.Equal(TransferOutcome.Success, transferResult.Outcome);

            var editingService = new ReportEditingService();
            var renameResult = editingService.RenameDisplayedTableColumn(project, report, table.Id, columnIndex: 0, newHeader: "Parça Adı");
            Assert.True(renameResult.IsSuccess);

            var registry = new DataProviderRegistry();
            registry.Register(new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance));
            var builder = new ReportContentBuilder(registry);
            var document = await builder.BuildAsync(project, report);

            var tableNode = Assert.IsType<TableContentNode>(document.BodyNodes[0]);
            Assert.Equal("Parça Adı", tableNode.ColumnHeaders[0]); // displayed header changed
            Assert.Equal("FromSheet1", Assert.Single(tableNode.Rows)[0]); // data still resolves correctly
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    private static string CreateTwoSheetWorkbook()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xlsx");

        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());

        AddSheet(workbookPart, sheets, "Sheet1", sheetId: 1, dataValue: "FromSheet1");
        AddSheet(workbookPart, sheets, "Sheet2", sheetId: 2, dataValue: "FromSheet2");

        workbookPart.Workbook.Save();
        return filePath;
    }

    private static void AddSheet(WorkbookPart workbookPart, Sheets sheets, string sheetName, uint sheetId, string dataValue)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet(sheetData);

        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = sheetId, Name = sheetName });

        var headerRow = new Row { RowIndex = 1 };
        headerRow.Append(BuildCell("A1", "Name"));
        headerRow.Append(BuildCell("B1", "Value"));

        var dataRow = new Row { RowIndex = 2 };
        dataRow.Append(BuildCell("A2", dataValue));
        dataRow.Append(BuildCell("B2", "1"));

        sheetData.Append(headerRow);
        sheetData.Append(dataRow);
    }

    private static Cell BuildCell(string cellReference, string value) =>
        new() { CellReference = cellReference, DataType = CellValues.String, CellValue = new CellValue(value) };
}
