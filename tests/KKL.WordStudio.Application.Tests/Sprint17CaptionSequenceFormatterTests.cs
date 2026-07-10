namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Formatting;
using Xunit;

public sealed class Sprint17CaptionSequenceFormatterTests
{
    private static TableCaptionSequenceProfile DefaultSequence() => new()
    {
        DisplayLabel = "Tablo",
        SequenceIdentifier = "Tablo",
        Separator = ": "
    };

    [Theory]
    [InlineData(1, "Tablo 1: İnsansız Hava Aracı")]
    [InlineData(2, "Tablo 2: İnsansız Hava Aracı")]
    public void BuildDisplayText_UsesAutomaticSequenceNumberAndConfiguredSeparator(
        int number,
        string expected)
    {
        var actual = TableCaptionSequenceFormatter.BuildDisplayText(
            "İnsansız Hava Aracı",
            DefaultSequence(),
            number);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveNextSequenceNumber_CountsCaptionedTablesAndSkipsBlankCaptions()
    {
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);
        var sequence = DefaultSequence();

        Assert.Equal(1, TableCaptionSequenceFormatter.ResolveNextSequenceNumber("Birinci", sequence, counters).GetValueOrDefault());
        Assert.Null(TableCaptionSequenceFormatter.ResolveNextSequenceNumber("   ", sequence, counters));
        Assert.Equal(2, TableCaptionSequenceFormatter.ResolveNextSequenceNumber("İkinci", sequence, counters).GetValueOrDefault());
        Assert.Equal(2, counters["Tablo"]);
    }

    [Fact]
    public void BuildDisplayText_ReplacesOnlyExactManualSequencePrefix()
    {
        var actual = TableCaptionSequenceFormatter.BuildDisplayText(
            "Tablo 7: İnsansız Hava Aracı",
            DefaultSequence(),
            2);

        Assert.Equal("Tablo 2: İnsansız Hava Aracı", actual);
    }

    [Theory]
    [InlineData("Model 7 İnsansız Hava Aracı")]
    [InlineData("Tablo X: İnsansız Hava Aracı")]
    [InlineData("Tablo 7. İnsansız Hava Aracı")]
    public void RemovePrefix_DoesNotInterpretUnrelatedDigitsOrDifferentShapes(string caption)
    {
        var actual = TableCaptionSequenceFormatter.RemoveDeterministicManualSequencePrefix(
            caption,
            DefaultSequence());

        Assert.Equal(caption, actual);
    }

    [Fact]
    public void BuildDisplayText_WithoutSequenceOrPositiveNumber_PreservesAuthoredCaption()
    {
        const string caption = "İnsansız Hava Aracı";

        Assert.Equal(caption, TableCaptionSequenceFormatter.BuildDisplayText(caption, null, 1));
        Assert.Equal(caption, TableCaptionSequenceFormatter.BuildDisplayText(caption, DefaultSequence(), null));
        Assert.Equal(caption, TableCaptionSequenceFormatter.BuildDisplayText(caption, DefaultSequence(), 0));
    }
}
