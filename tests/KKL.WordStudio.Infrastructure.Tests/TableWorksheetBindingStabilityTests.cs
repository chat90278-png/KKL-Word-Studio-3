namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.DataProviders;
using KKL.WordStudio.Infrastructure.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
// DocumentFormat.OpenXml.Spreadsheet defines its own Workbook/Worksheet/DataField/Page
// types (pivot-table/print concepts), which collide with our Domain types of the same
// name — the exact ambiguity already fixed in OpenXmlExcelWorkbookReader.cs (see ADR).
// Aliasing here the same way, rather than fully-qualifying every single usage below.
using DomainWorkbook = KKL.WordStudio.Domain.DataSources.Workbook;
using DomainWorksheet = KKL.WordStudio.Domain.DataSources.Worksheet;
using DomainDataField = KKL.WordStudio.Domain.DataBinding.DataField;
using DomainPage = KKL.WordStudio.Domain.Reports.Page;

/// <summary>
/// Validates the exact scenario required by the Variant 2.5 UI task before
/// any Table Binding UI could be built on top of it: two tables bound to
/// two different worksheets of the SAME ExcelDataSource must each keep
/// resolving their own worksheet, independent of which worksheet is
/// currently "active" (i.e. being browsed in the Excel Workspace).
///
/// Before the fix in this changeset, ExcelDataProvider read
/// ExcelDataSource.ActiveWorksheetName — a single mutable field shared by
/// every table bound to that DataSource — so switching the active
/// worksheet while browsing Sheet2 would silently make a table that was
/// supposed to show Sheet1 start showing Sheet2's rows instead. This test
/// fails against that old behavior and passes now that Binding carries its
/// own WorksheetName (see ADR 0009).
/// </summary>
public class TableWorksheetBindingStabilityTests
{
    [Fact]
    public async Task TablesBoundToDifferentWorksheets_StayStable_WhenActiveWorksheetChanges()
    {
        var workbookPath = CreateTwoSheetWorkbook();

        var dataSource = new ExcelDataSource
        {
            Name = "Aircraft",
            Workbook = new DomainWorkbook { FileName = "Aircraft.xlsx", SourcePath = workbookPath },
            ActiveWorksheetName = "Sheet1" // whatever the user currently happens to be browsing
        };
        dataSource.Workbook.Worksheets.Add(new DomainWorksheet
        {
            Name = "Sheet1",
            SelectedRange = new DataRange { HeaderRowIndex = 1, DataStartRow = 2, DataEndRow = 2 }
        });
        dataSource.Workbook.Worksheets.Add(new DomainWorksheet
        {
            Name = "Sheet2",
            SelectedRange = new DataRange { HeaderRowIndex = 1, DataStartRow = 2, DataEndRow = 2 }
        });
        dataSource.ColumnMappings.Add(new ColumnMapping { SourceColumn = "A", TargetField = new DomainDataField { Name = "Name", DataType = "Text" } });
        dataSource.ColumnMappings.Add(new ColumnMapping { SourceColumn = "B", TargetField = new DomainDataField { Name = "Value", DataType = "Text" } });

        var project = new Project { Name = "Aircraft Project" };
        project.DataSources.Add(dataSource);

        var report = new Report { Name = "Report" };
        var page = new DomainPage();
        var section = new Section { Kind = SectionKind.Body };

        var table1 = new TableElement { Name = "Table1", Binding = new Binding { DataSourceName = "Aircraft", WorksheetName = "Sheet1" } };
        var table2 = new TableElement { Name = "Table2", Binding = new Binding { DataSourceName = "Aircraft", WorksheetName = "Sheet2" } };
        section.Root.Children.Add(table1);
        section.Root.Children.Add(table2);
        page.Sections.Add(section);
        report.Pages.Add(page);
        project.Reports.Add(report);

        var registry = new DataProviderRegistry();
        registry.Register(new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance));
        var builder = new ReportContentBuilder(registry);

        // Simulate the user actively browsing Sheet2 in the Excel Workspace
        // (the exact action the task describes as "changes the active Excel
        // worksheet repeatedly") while the report is (re)built for preview/export.
        dataSource.ActiveWorksheetName = "Sheet2";
        var documentWhileBrowsingSheet2 = await builder.BuildAsync(project, report);

        dataSource.ActiveWorksheetName = "Sheet1";
        var documentWhileBrowsingSheet1 = await builder.BuildAsync(project, report);

        foreach (var document in new[] { documentWhileBrowsingSheet2, documentWhileBrowsingSheet1 })
        {
            var table1Node = Assert.IsType<TableContentNode>(document.BodyNodes[0]);
            var table2Node = Assert.IsType<TableContentNode>(document.BodyNodes[1]);

            Assert.Equal("FromSheet1", Assert.Single(table1Node.Rows)[0]);
            Assert.Equal("FromSheet2", Assert.Single(table2Node.Rows)[0]);
        }

        File.Delete(workbookPath);
    }

    /// <summary>
    /// Second scenario required by the Variant 2.5 task: a real report of a
    /// mapped header row being followed by the original Excel header text as
    /// the first "data" row was observed during manual testing. This proves
    /// whether that's a genuine ExcelDataProvider defect or a misconfigured
    /// DataRange — with HeaderRowIndex=2 and DataStartRow=3 explicitly set,
    /// row 2 (the header) must never appear among the returned data rows.
    /// </summary>
    [Fact]
    public async Task HeaderRow_IsNeverReturned_WhenDataStartRowIsAfterIt()
    {
        var workbookPath = CreateSheetWithBlankFirstRow();

        var dataSource = new ExcelDataSource
        {
            Name = "Aircraft",
            Workbook = new DomainWorkbook { FileName = "Aircraft.xlsx", SourcePath = workbookPath },
            ActiveWorksheetName = "Sheet1"
        };
        dataSource.Workbook.Worksheets.Add(new DomainWorksheet
        {
            Name = "Sheet1",
            SelectedRange = new DataRange { HeaderRowIndex = 2, DataStartRow = 3, DataEndRow = 4 }
        });
        dataSource.ColumnMappings.Add(new ColumnMapping { SourceColumn = "A", TargetField = new DomainDataField { Name = "Name", DataType = "Text" } });

        var provider = new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance);
        var result = await provider.GetRowsAsync(dataSource, worksheetNameOverride: "Sheet1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.DoesNotContain(result.Value, row => Equals(row["Name"], "Name")); // the header text itself
        Assert.Equal("FirstDataRow", result.Value[0]["Name"]);
        Assert.Equal("SecondDataRow", result.Value[1]["Name"]);

        File.Delete(workbookPath);
    }

    private static string CreateSheetWithBlankFirstRow()
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

        // Row 1: a title row above the real header (a common real-world layout).
        var titleRow = new Row { RowIndex = 1 };
        titleRow.Append(BuildCell("A1", "Aircraft Parts Report"));
        sheetData.Append(titleRow);

        // Row 2: the actual header row.
        var headerRow = new Row { RowIndex = 2 };
        headerRow.Append(BuildCell("A2", "Name"));
        sheetData.Append(headerRow);

        // Rows 3-4: real data.
        var row3 = new Row { RowIndex = 3 };
        row3.Append(BuildCell("A3", "FirstDataRow"));
        sheetData.Append(row3);

        var row4 = new Row { RowIndex = 4 };
        row4.Append(BuildCell("A4", "SecondDataRow"));
        sheetData.Append(row4);

        workbookPart.Workbook.Save();
        return filePath;
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

    // ExcelDataProvider maps cells to columns via Cell.CellReference (e.g. "A1") —
    // exactly how real Excel files are always structured. Every test cell must set
    // it explicitly; omitting it (as an earlier version of this fixture did) makes
    // the provider skip the cell entirely, which is what caused both tests to see
    // empty/null values instead of a real Windows test run pointed out.
    private static Cell BuildCell(string cellReference, string value) =>
        new() { CellReference = cellReference, DataType = CellValues.String, CellValue = new CellValue(value) };
}
