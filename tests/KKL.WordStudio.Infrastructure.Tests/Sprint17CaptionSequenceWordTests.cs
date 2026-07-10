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
