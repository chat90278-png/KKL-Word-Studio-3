namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Xunit;

public sealed class Sprint23TableHeadingWordPageBreakTests
{
    [Theory]
    [InlineData(ReportContentKind.Heading)]
    [InlineData(ReportContentKind.AltHeading)]
    public void HeadingAfterTable_UsesNativePageBreakBeforeWithoutBlankParagraph(
        ReportContentKind headingKind)
    {
        var body = new Body();
        WordContentWriter.AppendNode(body, CreateTable(), null, startOnNewPage: false);
        WordContentWriter.AppendNode(body, CreateHeading(headingKind), null, startOnNewPage: true);

        var heading = body.Elements<Paragraph>()
            .Single(paragraph => paragraph.InnerText == "Heading after table");

        Assert.NotNull(heading.ParagraphProperties?.GetFirstChild<PageBreakBefore>());
        Assert.DoesNotContain(
            body.Descendants<Break>(),
            br => br.Type?.Value == BreakValues.Page);
        Assert.Equal(2, body.ChildElements.Count);
    }

    [Fact]
    public void HeadingWithoutTableBreak_DoesNotReceivePageBreakBefore()
    {
        var body = new Body();
        WordContentWriter.AppendNode(
            body,
            CreateHeading(ReportContentKind.Heading),
            null,
            startOnNewPage: false);

        var heading = Assert.Single(body.Elements<Paragraph>());
        Assert.Null(heading.ParagraphProperties?.GetFirstChild<PageBreakBefore>());
    }

    private static TableContentNode CreateTable() => new()
    {
        ElementId = Guid.NewGuid(),
        Kind = ReportContentKind.Table,
        Name = "Table 1",
        ColumnHeaders = ["No", "Part Name"],
        Rows = [["1", "Part"]],
        SourceCount = 0
    };

    private static TextContentNode CreateHeading(ReportContentKind kind) => new()
    {
        ElementId = Guid.NewGuid(),
        Kind = kind,
        Text = "Heading after table"
    };
}
