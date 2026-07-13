namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using KKL.WordStudio.Infrastructure.Excel;
using KKL.WordStudio.Infrastructure.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class Sprint23ExcelReaderCharacterizationTests
{
    private static OpenXmlExcelWorkbookReader CreateReader() =>
        new(NullLogger<OpenXmlExcelWorkbookReader>.Instance);

    [Fact]
    public async Task DefaultSheetPreview_IsCurrentlyTruncatedAtOneHundredRows()
    {
        var filePath = Sprint22WorkbookFixtureFactory.Create(Sprint22WorkbookScenario.NormalSixColumn);
        try
        {
            var result = await CreateReader().GetSheetPreviewAsync(
                filePath,
                Sprint22WorkbookFixtureFactory.WorksheetName(1));

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(100, result.Value.Rows.Count);
            Assert.True(result.Value.IsTruncated);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DataEndDetection_CurrentlyStopsAtFirstBlankRowAfterData()
    {
        var filePath = CreateWorkbookWithBlankRowInsideData();
        try
        {
            var result = await CreateReader().DetectDataRangeAsync(
                filePath,
                "Sheet1",
                dataStartRow: 2,
                startColumn: 1,
                endColumn: 2);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(2, result.Value.DataEndRow);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string CreateWorkbookWithBlankRowInsideData()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"kkl-sprint23-characterization-{Guid.NewGuid():N}.xlsx");
        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        sheetData.Append(Row(1, ("A1", "No"), ("B1", "Part Name")));
        sheetData.Append(Row(2, ("A2", "1"), ("B2", "Valve")));
        sheetData.Append(new DocumentFormat.OpenXml.Spreadsheet.Row { RowIndex = 3U });
        sheetData.Append(Row(4, ("A4", "2"), ("B4", "Pump")));

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = "Sheet1"
        });

        worksheetPart.Worksheet.Save();
        workbookPart.Workbook.Save();
        return filePath;
    }

    private static DocumentFormat.OpenXml.Spreadsheet.Row Row(
        uint rowIndex,
        params (string Reference, string Value)[] cells)
    {
        var row = new DocumentFormat.OpenXml.Spreadsheet.Row { RowIndex = rowIndex };
        foreach (var cell in cells)
        {
            row.Append(new Cell
            {
                CellReference = cell.Reference,
                DataType = CellValues.String,
                CellValue = new CellValue(cell.Value)
            });
        }
        return row;
    }
}
