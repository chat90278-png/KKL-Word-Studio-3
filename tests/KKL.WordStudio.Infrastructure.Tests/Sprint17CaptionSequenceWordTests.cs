namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Xunit;

public sealed class Sprint17CaptionSequenceWordTests
{
    [Fact]
    public void BuiltInCaption_KeepsRealSeqFieldAndColonSeparator()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var sequence = profile.TableCaptionSequence!;

        var paragraph = WordParagraphWriter.BuildTableCaptionParagraph(
            "İnsansız Hava Aracı",
            sequence,
            profile.TableCaption);
        var field = Assert.Single(paragraph.Descendants<SimpleField>());

        Assert.Contains("SEQ Tablo", field.Instruction!.Value, StringComparison.Ordinal);
        Assert.Contains("ARABIC", field.Instruction.Value, StringComparison.Ordinal);
        Assert.StartsWith("Tablo ", paragraph.InnerText);
        Assert.Contains(": İnsansız Hava Aracı", paragraph.InnerText, StringComparison.Ordinal);
        Assert.DoesNotContain("Tablo 1: Tablo", paragraph.InnerText, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticSequenceNumber_IsWrittenAsTheSeqFieldsCachedResult()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var first = WordParagraphWriter.BuildTableCaptionParagraph(
            "Birinci",
            profile.TableCaptionSequence,
            profile.TableCaption,
            1);
        var second = WordParagraphWriter.BuildTableCaptionParagraph(
            "İkinci",
            profile.TableCaptionSequence,
            profile.TableCaption,
            2);

        Assert.Equal("1", Assert.Single(first.Descendants<SimpleField>()).InnerText);
        Assert.Equal("2", Assert.Single(second.Descendants<SimpleField>()).InnerText);
        Assert.Contains("SEQ Tablo", Assert.Single(second.Descendants<SimpleField>()).Instruction!.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualBuiltInPrefix_IsRemovedBeforeRealSeqFieldDescription()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();

        var paragraph = WordParagraphWriter.BuildTableCaptionParagraph(
            "Tablo 7: İnsansız Hava Aracı",
            profile.TableCaptionSequence,
            profile.TableCaption);

        Assert.Single(paragraph.Descendants<SimpleField>());
        Assert.Contains(": İnsansız Hava Aracı", paragraph.InnerText, StringComparison.Ordinal);
        Assert.DoesNotContain("Tablo 7:", paragraph.InnerText, StringComparison.Ordinal);
    }
}
