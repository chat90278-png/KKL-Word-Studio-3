namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using Xunit;

public sealed class Sprint23SemanticFieldMatcherTests
{
    private readonly ExcelSemanticFieldMatcher _matcher = new();

    [Theory]
    [InlineData("NO", ExcelSemanticFieldRole.ItemNumber)]
    [InlineData("No.", ExcelSemanticFieldRole.ItemNumber)]
    [InlineData("Numara", ExcelSemanticFieldRole.ItemNumber)]
    [InlineData("Part Name (English)", ExcelSemanticFieldRole.PartNameEnglish)]
    [InlineData("Parça Adı İngilizce", ExcelSemanticFieldRole.PartNameEnglish)]
    [InlineData("Türkçe Parça Adı", ExcelSemanticFieldRole.PartNameTurkish)]
    [InlineData("Tr İsim", ExcelSemanticFieldRole.PartNameTurkish)]
    [InlineData("Part No", ExcelSemanticFieldRole.PartNumber)]
    [InlineData("Parça Numarası", ExcelSemanticFieldRole.PartNumber)]
    [InlineData("N.S.N.", ExcelSemanticFieldRole.Nsn)]
    [InlineData("Serial Number", ExcelSemanticFieldRole.SerialNumber)]
    [InlineData("Seri Numarası", ExcelSemanticFieldRole.SerialNumber)]
    [InlineData("Qty", ExcelSemanticFieldRole.Quantity)]
    [InlineData("Miktar", ExcelSemanticFieldRole.Quantity)]
    public void Match_NormalizesTurkishEnglishPunctuationAndCase(
        string header,
        ExcelSemanticFieldRole expected)
    {
        Assert.Equal(expected, _matcher.Match(header));
    }

    [Fact]
    public void Match_LeavesUnknownHeadersForManualSelection()
    {
        Assert.Equal(ExcelSemanticFieldRole.Unknown, _matcher.Match("Bakım Açıklaması"));
    }

    [Fact]
    public void MatchRow_PreservesOriginalHeaderTextAndOneBasedColumnIdentity()
    {
        var matches = _matcher.MatchRow(
            ["No", "English Part Name", "Parça No", "NSN", "Seri No", "Adet", "Not"]);

        Assert.Equal(6, matches.Count);
        Assert.Equal(1, matches[0].ColumnIndex);
        Assert.Equal("No", matches[0].HeaderText);
        Assert.Equal(ExcelSemanticFieldRole.ItemNumber, matches[0].Role);
        Assert.Equal(6, matches[^1].ColumnIndex);
        Assert.Equal(ExcelSemanticFieldRole.Quantity, matches[^1].Role);
    }

    [Fact]
    public void RangeDetector_PrefersSemanticHeaderRowAfterLeadingDocumentTitle()
    {
        var detector = new ExcelDataRangeDetector();
        var result = detector.Detect(new SheetPreview
        {
            WorksheetName = "Sheet1",
            RowNumbers = [1, 3, 4, 5],
            Rows =
            [
                ["SYSTEM TEST CONFIGURATION LIST"],
                ["No", "Part Name (English)", "Part No", "NSN", "Serial No", "Qty"],
                ["1", "Valve", "PN-1", "100-10-10", "SN-1", "2"],
                ["2", "Pump", "PN-2", "100-10-20", "SN-2", "1"]
            ],
            ColumnCount = 6,
            IsTruncated = false
        });

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Value.HeaderRowIndex);
        Assert.Equal(4, result.Value.DataStartRow);
        Assert.Equal(ExcelDataRangeConfidence.High, result.Value.Confidence);
        Assert.Equal(6, result.Value.SemanticMatchCount);
    }

    [Fact]
    public void RangeDetector_DetectsBothPartNameLanguagesWithoutDiscardingEither()
    {
        var detector = new ExcelDataRangeDetector();
        var result = detector.Detect(new SheetPreview
        {
            WorksheetName = "Sheet1",
            RowNumbers = [2, 3, 4],
            Rows =
            [
                ["No", "Part Name", "Parça Adı", "Part Number", "NSN", "Serial Number", "Quantity"],
                ["1", "Valve", "Vana", "PN-1", "100", "SN-1", "2"],
                ["2", "Pump", "Pompa", "PN-2", "200", "SN-2", "1"]
            ],
            ColumnCount = 7,
            IsTruncated = false
        });

        Assert.True(result.IsSuccess, result.Error);
        Assert.Contains(result.Value.SemanticFields, field => field.Role == ExcelSemanticFieldRole.PartNameEnglish);
        Assert.Contains(result.Value.SemanticFields, field => field.Role == ExcelSemanticFieldRole.PartNameTurkish);
    }
}
