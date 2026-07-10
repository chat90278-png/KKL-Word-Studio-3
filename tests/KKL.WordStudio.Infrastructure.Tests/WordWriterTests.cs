namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Xunit;

public class WordWriterTests
{
    [Fact]
    public void WordParagraphWriter_Heading_GetsHeading1StyleId()
    {
        var node = new TextContentNode { ElementId = Guid.NewGuid(), Kind = ReportContentKind.Heading, Text = "Title", FontSize = 18, Bold = true };
        var paragraph = WordParagraphWriter.BuildParagraph(node);

        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        Assert.Equal("Heading1", styleId);
    }

    [Fact]
    public void WordParagraphWriter_AltHeading_GetsHeading2StyleId()
    {
        var node = new TextContentNode { ElementId = Guid.NewGuid(), Kind = ReportContentKind.AltHeading, Text = "Subtitle", FontSize = 14, Bold = true };
        var paragraph = WordParagraphWriter.BuildParagraph(node);

        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        Assert.Equal("Heading2", styleId);
    }

    [Fact]
    public void WordParagraphWriter_PlainParagraph_HasNoParagraphStyleId()
    {
        var node = new TextContentNode { ElementId = Guid.NewGuid(), Kind = ReportContentKind.Paragraph, Text = "Body text", FontSize = 10, Bold = false };
        var paragraph = WordParagraphWriter.BuildParagraph(node);

        Assert.Null(paragraph.ParagraphProperties?.ParagraphStyleId);
    }

    [Fact]
    public void WordParagraphWriter_TocParagraph_ContainsTocFieldInstruction()
    {
        var paragraph = WordParagraphWriter.BuildTocParagraph();
        var field = paragraph.Descendants<SimpleField>().Single();

        Assert.Contains("TOC", field.Instruction!.Value);
    }

    [Fact]
    public void WordStyleWriter_Heading1And2_HaveDistinctOutlineLevels()
    {
        using var stream = new MemoryStream();
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        WordStyleWriter.AddStyleDefinitions(mainPart);

        var styles = mainPart.StyleDefinitionsPart!.Styles!.Elements<Style>().ToList();
        var heading1 = styles.Single(s => s.StyleId == "Heading1");
        var heading2 = styles.Single(s => s.StyleId == "Heading2");

        Assert.Equal(0, heading1.StyleParagraphProperties!.OutlineLevel!.Val!.Value);
        Assert.Equal(1, heading2.StyleParagraphProperties!.OutlineLevel!.Val!.Value);
    }

    [Fact]
    public void WordPageLayoutWriter_ConvertsMillimetersToTwipsCorrectly()
    {
        var sectionProperties = new SectionProperties();
        var layout = new PageLayout
        {
            WidthMillimeters = 210,
            HeightMillimeters = 297,
            MarginTopMillimeters = 20,
            MarginBottomMillimeters = 20,
            MarginLeftMillimeters = 25,
            MarginRightMillimeters = 25,
            ShowPageNumbers = true
        };

        WordPageLayoutWriter.AppendPageLayout(sectionProperties, layout);

        var pageSize = sectionProperties.GetFirstChild<PageSize>()!;
        var pageMargin = sectionProperties.GetFirstChild<PageMargin>()!;

        // 210mm * 1440/25.4 ≈ 11906 twips (A4 width)
        Assert.Equal(11906u, pageSize.Width!.Value);
        Assert.Equal(16838u, pageSize.Height!.Value);
        Assert.Equal(1417u, pageMargin.Left!.Value);   // 25mm
        Assert.Equal(1417u, pageMargin.Right!.Value);
    }
}
