namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint24HeadingChainPaginationTests
{
    [Fact]
    public async Task PublicLayout_CarriesWholeTrailingHeadingChainWithFollowingTableStart()
    {
        var spacerId = Guid.NewGuid();
        var headingId = Guid.NewGuid();
        var altHeadingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var document = CreateDocument(
            pageHeightMillimeters: 80d,
            bodyNodes:
            [
                new ImageContentNode
                {
                    ElementId = spacerId,
                    Kind = ReportContentKind.Image,
                    Name = "Deterministic spacer"
                },
                CreateText(headingId, ReportContentKind.Heading, "Heading"),
                CreateText(altHeadingId, ReportContentKind.AltHeading, "Alt heading"),
                CreateTable(tableId, rowCount: 3)
            ]);

        var layout = await LayoutAsync(document);
        var spacerPage = FirstPageFor(layout, spacerId);
        var headingPage = FirstPageFor(layout, headingId);
        var altHeadingPage = FirstPageFor(layout, altHeadingId);
        var tablePage = FirstPageFor(layout, tableId);

        Assert.True(headingPage > spacerPage);
        Assert.Equal(headingPage, altHeadingPage);
        Assert.Equal(headingPage, tablePage);
        Assert.All(layout.Pages, AssertHasBodyContent);
    }

    [Fact]
    public async Task PublicLayout_DoesNotCreateEmptyIntermediatePageWhenHeadingStartsFreshPage()
    {
        var headingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var document = CreateDocument(
            pageHeightMillimeters: 55d,
            bodyNodes:
            [
                CreateText(headingId, ReportContentKind.Heading, "Heading"),
                CreateTable(tableId, rowCount: 12)
            ]);

        var layout = await LayoutAsync(document);

        Assert.Equal(1, FirstPageFor(layout, headingId));
        Assert.True(FirstPageFor(layout, tableId) >= FirstPageFor(layout, headingId));
        Assert.All(layout.Pages, AssertHasBodyContent);
        Assert.Equal(
            Enumerable.Range(1, layout.Pages.Count),
            layout.Pages.Select(page => page.PageNumber));
    }

    private static Task<DocumentLayoutResult> LayoutAsync(ReportContentDocument document) =>
        new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

    private static ReportContentDocument CreateDocument(
        double pageHeightMillimeters,
        IReadOnlyList<ReportContentNode> bodyNodes) => new()
    {
        HeaderNodes = [],
        BodyNodes = bodyNodes,
        FooterNodes = [],
        TableOfContents = [],
        PageLayout = new PageLayout
        {
            WidthMillimeters = 210d,
            HeightMillimeters = pageHeightMillimeters,
            MarginTopMillimeters = 10d,
            MarginBottomMillimeters = 10d,
            MarginLeftMillimeters = 20d,
            MarginRightMillimeters = 20d,
            ShowPageNumbers = false
        }
    };

    private static TextContentNode CreateText(
        Guid elementId,
        ReportContentKind kind,
        string text) => new()
    {
        ElementId = elementId,
        Kind = kind,
        Text = text
    };

    private static TableContentNode CreateTable(Guid elementId, int rowCount) => new()
    {
        ElementId = elementId,
        Kind = ReportContentKind.Table,
        Name = "Pagination table",
        Caption = "Pagination table",
        ColumnHeaders = ["No", "Part", "Quantity"],
        Rows = Enumerable.Range(1, rowCount)
            .Select(index => (IReadOnlyList<string>)[index.ToString(), $"Part {index}", "1"])
            .ToList(),
        SourceCount = 0
    };

    private static int FirstPageFor(DocumentLayoutResult layout, Guid elementId) =>
        layout.Pages
            .Where(page => page.Blocks.Any(block => block.ElementId == elementId))
            .Select(page => page.PageNumber)
            .First();

    private static void AssertHasBodyContent(DocumentPageLayout page) =>
        Assert.Contains(page.Blocks, block => block.Region == DocumentPageRegion.Body);
}
