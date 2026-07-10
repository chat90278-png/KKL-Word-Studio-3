namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using Xunit;

public sealed class Sprint17AutoRangeRegressionTests
{
    private readonly ExcelDataRangeDetector _detector = new();

    [Fact]
    public void AutoRange_IncludesSparseTrailingHeaderColumn_WhenAnyDataValueExists()
    {
        var result = _detector.Detect(Preview(
            (3, "No", "Tr İsim", "Parça Numarası", "NSN", "Seri Numarası", "Adet"),
            (4, "1", "elma", "1234", "45-50-60", "9999", "1"),
            (5, "2", "armut", "56789", "459-485-5", "9988", "2"),
            (6, "2", "armut", "56789", "459-485-5", "9987", ""),
            (7, "3", "kiraz", "90001", "100-200-3", "7001", ""),
            (8, "4", "muz", "90002", "100-200-4", "7002", ""),
            (9, "5", "erik", "90003", "100-200-5", "7003", ""),
            (10, "6", "ayva", "90004", "100-200-6", "7004", ""),
            (11, "7", "üzüm", "90005", "100-200-7", "7005", ""),
            (12, "8", "incir", "90006", "100-200-8", "7006", ""),
            (13, "9", "kivi", "90007", "100-200-9", "7007", "")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Value.HeaderRowIndex);
        Assert.Equal(4, result.Value.DataStartRow);
        Assert.Equal(1, result.Value.StartColumn);
        Assert.Equal(6, result.Value.EndColumn);
    }

    [Fact]
    public void AutoRange_DoesNotIncludeTrailingHeaderOnlyColumn_WhenDataIsAlwaysBlank()
    {
        var result = _detector.Detect(Preview(
            (3, "No", "Tr İsim", "Parça Numarası", "NSN", "Seri Numarası", "Adet"),
            (4, "1", "elma", "1234", "45-50-60", "9999", ""),
            (5, "2", "armut", "56789", "459-485-5", "9988", ""),
            (6, "2", "armut", "56789", "459-485-5", "9987", "")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(5, result.Value.EndColumn);
    }

    private static SheetPreview Preview(params object[] rowDefinitions)
    {
        var rows = rowDefinitions
            .Select(definition => (System.Runtime.CompilerServices.ITuple)definition)
            .Select(tuple => new
            {
                RowNumber = Convert.ToInt32(tuple[0]),
                Values = Enumerable.Range(1, tuple.Length - 1)
                    .Select(index => tuple[index]?.ToString() ?? string.Empty)
                    .ToList()
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
}
