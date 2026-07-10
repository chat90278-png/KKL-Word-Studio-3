namespace KKL.WordStudio.Application.Excel;

/// <summary>Range-aware projection helpers shared by the Excel workflow and focused tests.</summary>
public static class ExcelRangeProjection
{
    public static IReadOnlyList<string> GetRowTexts(SheetPreview preview, int rowNumber, int startColumn, int endColumn)
    {
        if (startColumn < 1 || endColumn < startColumn) return Array.Empty<string>();
        var rowIndex = preview.RowNumbers.ToList().IndexOf(rowNumber);
        if (rowIndex < 0) return Array.Empty<string>();

        var cells = preview.Rows[rowIndex];
        var texts = new List<string>(endColumn - startColumn + 1);
        for (var sourceColumn = startColumn; sourceColumn <= endColumn; sourceColumn++)
        {
            var previewIndex = sourceColumn - 1;
            texts.Add(previewIndex >= 0 && previewIndex < cells.Count ? cells[previewIndex] : string.Empty);
        }
        return texts;
    }
}
