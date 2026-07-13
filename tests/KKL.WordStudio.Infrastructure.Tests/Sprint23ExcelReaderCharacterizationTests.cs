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
    public async Task DefaultSheetPreview_LoadsTheCompleteSourceInsteadOfStoppingAtOneHundredRows()
    {
        var scenario = Sprint22WorkbookScenario.NormalSixColumn;
        var filePath = Sprint22WorkbookFixtureFactory.Create(scenario);
        try
        {
            var result = await CreateReader().GetSheetPreviewAsync(
                filePath,
                Sprint22WorkbookFixtureFactory.WorksheetName(1));

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(scenario.DataRowCount + 1, result.Value.Rows.Count); // header + all data
            Assert.False(result.Value.IsTruncated);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task ExplicitPreviewBound_RemainsAvailableForSpecialisedLightweightReads()
    {
        var filePath = Sprint22WorkbookFixtureFactory.Create(Sprint22WorkbookScenario.NormalSixColumn);
        try
        {
            var result = await CreateReader().GetSheetPreviewAsync(
                filePath,
                Sprint22WorkbookFixtureFactory.WorksheetName(1),
                maxPreviewRows: 40);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(40, result.Value.Rows.Count);
            Assert.True(result.Value.IsTruncated);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DataEndDetection_ToleratesAnAccidentalBlankRowInsideData()
    {
        var filePath = CreateWorkbookWithRows(
            Row(1, ("A1", "No"), ("B1", "Part Name")),
            Row(2, ("A2", "1"), ("B2", "Valve")),
            new DocumentFormat.OpenXml.Spreadsheet.Row { RowIndex = 3U },
            Row(4, ("A4", "2"), ("B4", "Pump")));
        try
        {
            var result = await CreateReader().DetectDataRangeAsync(
                filePath,
                "Sheet1",
                dataStartRow: 2,
                startColumn: 1,
                endColumn: 2);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(4, result.Value.DataEndRow);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DataEndDetection_StopsAtTheLastMeaningfulRowBeforeASustainedBlankGap()
    {
        var filePath = CreateWorkbookWithRows(
            Row(1, ("A1", "No"), ("B1", "Part Name")),
            Row(2, ("A2", "1"), ("B2", "Valve")),
            Row(8, ("A8", "2"), ("B8", "Unrelated later block")));
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

    [Fact]
    public async Task DataEndDetection_TreatsFormulaCellsAsMeaningfulEvenWithoutCachedDisplayText()
    {
        var formulaRow = new DocumentFormat.OpenXml.Spreadsheet.Row { RowIndex = 3U };
        formulaRow.Append(new Cell
        {
            CellReference = "B3",
            CellFormula = new CellFormula("B2")
        });

        var filePath = CreateWorkbookWithRows(
            Row(1, ("A1", "No"), ("B1", "Part Name")),
            Row(2, ("A2", "1"), ("B2", "Valve")),
            formulaRow);
        try
        {
            var result = await CreateReader().DetectDataRangeAsync(
                filePath,
                "Sheet1",
                dataStartRow: 2,
                startColumn: 1,
                endColumn: 2);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(3, result.Value.DataEndRow);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string CreateWorkbookWithRows(params DocumentFormat.OpenXml.Spreadsheet.Row[] rows)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"kkl-sprint23-range-{Guid.NewGuid():N}.xlsx");
        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        foreach (var row in rows)
            sheetData.Append(row);

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
