namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Infrastructure.Excel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainWorkbook = KKL.WordStudio.Domain.DataSources.Workbook;
using DomainWorksheet = KKL.WordStudio.Domain.DataSources.Worksheet;
using DomainDataField = KKL.WordStudio.Domain.DataBinding.DataField;

public class WorksheetMappingIndependenceTests
{
    [Fact]
    public async Task WorksheetMappings_AreIndependentPerWorksheet()
    {
        var workbookPath = CreateWorkbook();
        try
        {
            var sheet1 = new DomainWorksheet
            {
                Name = "Sheet1",
                SelectedRange = new DataRange { HeaderRowIndex = 1, DataStartRow = 2, DataEndRow = 2, StartColumn = 2, EndColumn = 2 }
            };
            sheet1.ColumnMappings.Add(new ColumnMapping
            {
                SourceColumn = "B",
                TargetField = new DomainDataField { Name = "PartName", DataType = "Text" }
            });

            var sheet2 = new DomainWorksheet
            {
                Name = "Sheet2",
                SelectedRange = new DataRange { HeaderRowIndex = 1, DataStartRow = 2, DataEndRow = 2, StartColumn = 2, EndColumn = 2 }
            };
            sheet2.ColumnMappings.Add(new ColumnMapping
            {
                SourceColumn = "B",
                TargetField = new DomainDataField { Name = "EngineType", DataType = "Text" }
            });

            var dataSource = new ExcelDataSource
            {
                Name = "Workbook",
                Workbook = new DomainWorkbook { FileName = "Workbook.xlsx", SourcePath = workbookPath },
                ActiveWorksheetName = "Sheet1"
            };
            dataSource.Workbook.Worksheets.Add(sheet1);
            dataSource.Workbook.Worksheets.Add(sheet2);

            var provider = new ExcelDataProvider(NullLogger<ExcelDataProvider>.Instance);
            var sheet1Rows = await provider.GetRowsAsync(dataSource, worksheetNameOverride: "Sheet1");
            var sheet2Rows = await provider.GetRowsAsync(dataSource, worksheetNameOverride: "Sheet2");

            Assert.True(sheet1Rows.IsSuccess);
            Assert.True(sheet2Rows.IsSuccess);

            var sheet1Row = Assert.Single(sheet1Rows.Value);
            var sheet2Row = Assert.Single(sheet2Rows.Value);
            Assert.Equal("Flap", sheet1Row["PartName"]);
            Assert.False(sheet1Row.ContainsKey("EngineType"));
            Assert.Equal("Turbofan", sheet2Row["EngineType"]);
            Assert.False(sheet2Row.ContainsKey("PartName"));
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    private static string CreateWorkbook()
    {
        var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xlsx");
        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());

        AddSheet(workbookPart, sheets, "Sheet1", 1, "Flap");
        AddSheet(workbookPart, sheets, "Sheet2", 2, "Turbofan");
        workbookPart.Workbook.Save();
        return filePath;
    }

    private static void AddSheet(WorkbookPart workbookPart, Sheets sheets, string name, uint id, string value)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var data = new SheetData();
        worksheetPart.Worksheet = new DocumentFormat.OpenXml.Spreadsheet.Worksheet(data);
        sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = id, Name = name });

        var headerRow = new Row { RowIndex = 1 };
        headerRow.Append(BuildCell("A1", "Code"));
        headerRow.Append(BuildCell("B1", "Meaning"));
        data.Append(headerRow);

        var valueRow = new Row { RowIndex = 2 };
        valueRow.Append(BuildCell("A2", "X"));
        valueRow.Append(BuildCell("B2", value));
        data.Append(valueRow);

        worksheetPart.Worksheet.Save();
    }

    private static Cell BuildCell(string reference, string value) =>
        new() { CellReference = reference, DataType = CellValues.String, CellValue = new CellValue(value) };
}
