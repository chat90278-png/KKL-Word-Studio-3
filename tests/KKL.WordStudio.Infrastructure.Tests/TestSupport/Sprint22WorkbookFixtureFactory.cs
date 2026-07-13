namespace KKL.WordStudio.Infrastructure.Tests.TestSupport;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

internal sealed record Sprint22WorkbookScenario(
    string Name,
    int WorksheetCount,
    int DataRowCount,
    int ColumnCount,
    bool IncludeSparseCells = false,
    bool IncludeDirtyValues = false)
{
    public static Sprint22WorkbookScenario NormalSixColumn { get; } =
        new("normal-six-column", WorksheetCount: 1, DataRowCount: 250, ColumnCount: 6);

    public static Sprint22WorkbookScenario ThousandsOfRows { get; } =
        new("thousands-of-rows", WorksheetCount: 1, DataRowCount: 2_500, ColumnCount: 12);

    public static Sprint22WorkbookScenario VeryWide { get; } =
        new("very-wide", WorksheetCount: 1, DataRowCount: 64, ColumnCount: 160);

    public static Sprint22WorkbookScenario ManyWorksheets { get; } =
        new("many-worksheets", WorksheetCount: 30, DataRowCount: 40, ColumnCount: 8);

    public static Sprint22WorkbookScenario SparseDirty { get; } =
        new(
            "sparse-dirty",
            WorksheetCount: 1,
            DataRowCount: 1_800,
            ColumnCount: 24,
            IncludeSparseCells: true,
            IncludeDirtyValues: true);
}

internal static class Sprint22WorkbookFixtureFactory
{
    public static string Create(Sprint22WorkbookScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.WorksheetCount <= 0) throw new ArgumentOutOfRangeException(nameof(scenario));
        if (scenario.DataRowCount <= 0) throw new ArgumentOutOfRangeException(nameof(scenario));
        if (scenario.ColumnCount <= 0) throw new ArgumentOutOfRangeException(nameof(scenario));

        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"kkl-sprint22-{scenario.Name}-{Guid.NewGuid():N}.xlsx");

        using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());

        for (var worksheetIndex = 1; worksheetIndex <= scenario.WorksheetCount; worksheetIndex++)
        {
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var worksheetName = WorksheetName(worksheetIndex);
            sheets.Append(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = (uint)worksheetIndex,
                Name = worksheetName
            });

            sheetData.Append(BuildHeaderRow(scenario.ColumnCount));
            for (var dataRowIndex = 1; dataRowIndex <= scenario.DataRowCount; dataRowIndex++)
                sheetData.Append(BuildDataRow(scenario, worksheetIndex, dataRowIndex));

            worksheetPart.Worksheet.Save();
        }

        workbookPart.Workbook.Save();
        return filePath;
    }

    public static string WorksheetName(int worksheetIndex) => $"Sheet{worksheetIndex:000}";

    public static string ExpectedValue(int worksheetIndex, int dataRowIndex, int columnIndex) =>
        $"S{worksheetIndex:000}-R{dataRowIndex:000000}-C{columnIndex:000}";

    private static Row BuildHeaderRow(int columnCount)
    {
        var row = new Row { RowIndex = 1U };
        for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++)
            row.Append(BuildCell(columnIndex, 1, $"Column{columnIndex:000}"));
        return row;
    }

    private static Row BuildDataRow(
        Sprint22WorkbookScenario scenario,
        int worksheetIndex,
        int dataRowIndex)
    {
        var rowIndex = dataRowIndex + 1;
        var row = new Row { RowIndex = (uint)rowIndex };

        for (var columnIndex = 1; columnIndex <= scenario.ColumnCount; columnIndex++)
        {
            if (scenario.IncludeSparseCells && ShouldOmitCell(dataRowIndex, columnIndex, scenario.ColumnCount))
                continue;

            var value = ExpectedValue(worksheetIndex, dataRowIndex, columnIndex);
            if (scenario.IncludeDirtyValues && (dataRowIndex + columnIndex) % 37 == 0)
                value = $"  {value}  ";

            row.Append(BuildCell(columnIndex, rowIndex, value));
        }

        return row;
    }

    private static bool ShouldOmitCell(int dataRowIndex, int columnIndex, int columnCount)
    {
        if (columnIndex == 1 || columnIndex == columnCount)
            return false;

        return (dataRowIndex * 17 + columnIndex * 13) % 29 == 0;
    }

    private static Cell BuildCell(int columnIndex, int rowIndex, string value) => new()
    {
        CellReference = $"{ToColumnLetters(columnIndex)}{rowIndex}",
        DataType = CellValues.String,
        CellValue = new CellValue(value)
    };

    private static string ToColumnLetters(int columnIndex)
    {
        var letters = string.Empty;
        var value = columnIndex;
        while (value > 0)
        {
            value--;
            letters = (char)('A' + value % 26) + letters;
            value /= 26;
        }

        return letters;
    }
}
