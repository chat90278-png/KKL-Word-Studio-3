namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint23TableHeadingPageBreakTests
{
    [Fact]
    public async Task HeadingAfterShortTable_StartsOnFollowingPageEvenWhenSpaceRemains()
    {
        var tableId = Guid.NewGuid();
        var headingId = Guid.NewGuid();
        var document = CreateDocument(tableId, headingId, rowCount: 2);

        var layout = await new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

        var tableLastPage = LastPageFor(layout, tableId);
        var headingFirstPage = FirstPageFor(layout, headingId);

        Assert.Equal(tableLastPage + 1, headingFirstPage);
        Assert.Equal(1, tableLastPage);
    }

    [Fact]
    public async Task HeadingAfterMultiPageTable_StartsAfterTheLastTableFragmentWithoutBlankPage()
    {
        var tableId = Guid.NewGuid();
        var headingId = Guid.NewGuid();
        var document = CreateDocument(tableId, headingId, rowCount: 90);

        var layout = await new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

        var tableLastPage = LastPageFor(layout, tableId);
        var headingFirstPage = FirstPageFor(layout, headingId);

        Assert.True(tableLastPage > 1);
        Assert.Equal(tableLastPage + 1, headingFirstPage);
        Assert.Contains(
            layout.Pages.Single(page => page.PageNumber == headingFirstPage).Blocks,
            block => block.ElementId == headingId && block.FragmentIndex == 0);
    }

    private static ReportContentDocument CreateDocument(
        Guid tableId,
        Guid headingId,
        int rowCount) => new()
    {
        HeaderNodes = [],
        BodyNodes =
        [
            new TableContentNode
            {
                ElementId = tableId,
                Kind = ReportContentKind.Table,
                Name = "Page break table",
                Caption = "Page break table",
                ColumnHeaders = ["No", "Part Name", "Quantity"],
                Rows = Enumerable.Range(1, rowCount)
                    .Select(index => (IReadOnlyList<string>)[index.ToString(), $"Part {index}", "1"])
                    .ToList(),
                SourceCount = 0
            },
            new TextContentNode
            {
                ElementId = headingId,
                Kind = ReportContentKind.Heading,
                Text = "Heading after table"
            }
        ],
        FooterNodes = [],
        TableOfContents = [],
        PageLayout = new PageLayout
        {
            WidthMillimeters = 210d,
            HeightMillimeters = 297d,
            MarginTopMillimeters = 20d,
            MarginBottomMillimeters = 20d,
            MarginLeftMillimeters = 20d,
            MarginRightMillimeters = 20d,
            ShowPageNumbers = false
        }
    };

    private static int FirstPageFor(DocumentLayoutResult layout, Guid elementId) =>
        layout.Pages
            .Where(page => page.Blocks.Any(block => block.ElementId == elementId))
            .Select(page => page.PageNumber)
            .First();

    private static int LastPageFor(DocumentLayoutResult layout, Guid elementId) =>
        layout.Pages
            .Where(page => page.Blocks.Any(block => block.ElementId == elementId))
            .Select(page => page.PageNumber)
            .Last();
}
