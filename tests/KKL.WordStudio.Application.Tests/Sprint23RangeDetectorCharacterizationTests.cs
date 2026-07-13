namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using Xunit;

public sealed class Sprint23RangeDetectorCharacterizationTests
{
    private readonly ExcelDataRangeDetector _detector = new();

    [Fact]
    public void StandardPartsHeader_IsDetectedAfterLeadingDocumentTitle()
    {
        var result = _detector.Detect(Preview(
            (1, "System Test Procedure Configuration List"),
            (2, "No", "Part Name", "Part Number", "NSN", "Serial Number", "Quantity"),
            (3, "1", "Valve", "PN-100", "1234-00-001", "SN-001", "1"),
            (4, "2", "Pump", "PN-200", "1234-00-002", "SN-002", "2")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value.HeaderRowIndex);
        Assert.Equal(3, result.Value.DataStartRow);
        Assert.Equal(1, result.Value.StartColumn);
        Assert.Equal(6, result.Value.EndColumn);
        Assert.Equal(ExcelDataRangeConfidence.High, result.Value.Confidence);
    }

    [Fact]
    public void TurkishPartsHeader_IsDetectedWithExpectedSixColumnShape()
    {
        var result = _detector.Detect(Preview(
            (5, "No", "Parça Adı Türkçe", "Parça Numarası", "NSN", "Seri Numarası", "Adet"),
            (6, "1", "Valf", "PN-100", "1234-00-001", "SN-001", "1"),
            (7, "2", "Pompa", "PN-200", "1234-00-002", "SN-002", "2")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(5, result.Value.HeaderRowIndex);
        Assert.Equal(6, result.Value.DataStartRow);
        Assert.Equal(1, result.Value.StartColumn);
        Assert.Equal(6, result.Value.EndColumn);
    }

    [Fact]
    public void PunctuationAndCaseVariants_StillLookLikeAHeaderToCurrentHeuristic()
    {
        var result = _detector.Detect(Preview(
            (10, "NO.", "PART NAME (ENGLISH)", "PART-NUMBER", "N.S.N.", "SERIAL NO", "QTY"),
            (11, "1", "Valve", "PN-100", "1234", "SN-001", "1"),
            (12, "2", "Pump", "PN-200", "5678", "SN-002", "2")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(10, result.Value.HeaderRowIndex);
        Assert.Equal(11, result.Value.DataStartRow);
        Assert.Equal(ExcelDataRangeConfidence.High, result.Value.Confidence);
    }

    [Fact]
    public void SingleDataRowWithoutHeader_IsLowConfidenceAndRequiresReview()
    {
        var result = _detector.Detect(Preview(
            (20, "1", "Valve", "PN-100")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value.HeaderRowIndex);
        Assert.Equal(20, result.Value.DataStartRow);
        Assert.Equal(ExcelDataRangeConfidence.Low, result.Value.Confidence);
        Assert.True(result.Value.RequiresReview);
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
