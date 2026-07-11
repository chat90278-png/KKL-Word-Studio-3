namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Formatting;
using Xunit;

public sealed class Sprint18CaptionSequencePeekTests
{
    [Fact]
    public void PeekNextSequenceNumber_ReturnsTheNextNumberWithoutConsumingTheCounter()
    {
        var sequence = DefaultDocumentFormatProfileFactory.Create().TableCaptionSequence!;
        var counters = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [sequence.SequenceIdentifier] = 4
        };

        var peeked = TableCaptionSequenceFormatter.PeekNextSequenceNumber(
            "Boundary caption",
            sequence,
            counters);

        Assert.Equal(5, peeked);
        Assert.Equal(4, counters[sequence.SequenceIdentifier]);

        var resolved = TableCaptionSequenceFormatter.ResolveNextSequenceNumber(
            "Boundary caption",
            sequence,
            counters);

        Assert.Equal(5, resolved);
        Assert.Equal(5, counters[sequence.SequenceIdentifier]);
    }

    [Fact]
    public void PeekNextSequenceNumber_BlankCaptionDoesNotCreateOrAdvanceASequenceCounter()
    {
        var sequence = DefaultDocumentFormatProfileFactory.Create().TableCaptionSequence!;
        var counters = new Dictionary<string, int>(StringComparer.Ordinal);

        var peeked = TableCaptionSequenceFormatter.PeekNextSequenceNumber(
            "   ",
            sequence,
            counters);

        Assert.Null(peeked);
        Assert.Empty(counters);
    }
}
