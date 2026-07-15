namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint24PaginationParityTests
{
    [Fact]
    public async Task HeadingAndAltHeading_MoveWithFollowingTableStart()
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
                new TextContentNode
                {
                    ElementId = headingId,
                    Kind = ReportContentKind.Heading,
                    Text = "Heading"
                },
                new TextContentNode
                {
                    ElementId = altHeadingId,
                    Kind = ReportContentKind.AltHeading,
                    Text = "Alt heading"
                },
                CreateTable(tableId, rowCount: 5, repeatHeader: true)
            ]);

        var layout = await LayoutAsync(document);

        var spacerPage = FirstPageFor(layout, spacerId);
        var headingPage = FirstPageFor(layout, headingId);
        var altHeadingPage = FirstPageFor(layout, altHeadingId);
        var tablePage = FirstPageFor(layout, tableId);
        var firstTableFragment = TableFragments(layout, tableId).First().Payload;

        Assert.True(headingPage > spacerPage);
        Assert.Equal(headingPage, altHeadingPage);
        Assert.Equal(headingPage, tablePage);
        Assert.NotNull(firstTableFragment.Caption);
        Assert.True(firstTableFragment.Rows.Count >= 3);
    }

    [Fact]
    public async Task ShortTable_RemainsOneFragmentWithCaptionAndRows()
    {
        var tableId = Guid.NewGuid();
        var layout = await LayoutAsync(CreateDocument(
            pageHeightMillimeters: 297d,
            bodyNodes: [CreateTable(tableId, rowCount: 2, repeatHeader: true)]));

        var fragment = Assert.Single(TableFragments(layout, tableId));

        Assert.Equal(0, fragment.Block.FragmentIndex);
        Assert.False(fragment.Block.IsContinuation);
        Assert.NotNull(fragment.Payload.Caption);
        Assert.Equal(2, fragment.Payload.Rows.Count);
        Assert.False(fragment.Payload.IsHeaderRepeated);
    }

    [Fact]
    public async Task LongTable_RepeatsHeadersAndPreservesEveryRowExactlyOnce()
    {
        var tableId = Guid.NewGuid();
        var document = CreateDocument(
            pageHeightMillimeters: 297d,
            bodyNodes: [CreateTable(tableId, rowCount: 100, repeatHeader: true)]);
        var layout = await LayoutAsync(document);
        var fragments = TableFragments(layout, tableId);
        var flattenedRowNumbers = fragments
            .SelectMany(fragment => fragment.Payload.Rows)
            .Select(row => int.Parse(row[0]))
            .ToArray();

        Assert.True(fragments.Count > 1);
        Assert.NotNull(fragments[0].Payload.Caption);
        Assert.All(fragments.Skip(1), fragment =>
        {
            Assert.Null(fragment.Payload.Caption);
            Assert.True(fragment.Payload.IsHeaderRepeated);
        });
        Assert.Equal(Enumerable.Range(1, 100), flattenedRowNumbers);
        Assert.Equal(100, fragments.Sum(fragment => fragment.Payload.Rows.Count));
        Assert.All(layout.Pages, page => Assert.Contains(
            page.Blocks,
            block => block.Region == DocumentPageRegion.Body));
    }

    [Fact]
    public async Task SameInput_ProducesSameFragmentPlan()
    {
        var tableId = Guid.NewGuid();
        var document = CreateDocument(
            pageHeightMillimeters: 180d,
            bodyNodes: [CreateTable(tableId, rowCount: 75, repeatHeader: true)]);

        var first = await LayoutAsync(document);
        var second = await LayoutAsync(document);

        Assert.Equal(FragmentSignature(first, tableId), FragmentSignature(second, tableId));
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

    private static TableContentNode CreateTable(Guid tableId, int rowCount, bool repeatHeader) => new()
    {
        ElementId = tableId,
        Kind = ReportContentKind.Table,
        Name = "Pagination table",
        Caption = "Pagination table",
        ColumnHeaders = ["No", "Part", "Quantity"],
        Rows = Enumerable.Range(1, rowCount)
            .Select(index => (IReadOnlyList<string>)[index.ToString(), $"Part {index}", "1"])
            .ToList(),
        SourceCount = 0,
        Format = new ResolvedTableFormat
        {
            WidthPercent = 100d,
            FixedLayout = true,
            BorderSizePoints = 0.5d,
            CellMarginTopMillimeters = 0d,
            CellMarginBottomMillimeters = 0d,
            CellMarginLeftMillimeters = 0d,
            CellMarginRightMillimeters = 0d,
            PreferredRowHeightMillimeters = 0d,
            RepeatHeader = repeatHeader,
            Columns = Array.Empty<ResolvedTableColumnFormat>()
        }
    };

    private static int FirstPageFor(DocumentLayoutResult layout, Guid elementId) =>
        layout.Pages
            .Where(page => page.Blocks.Any(block => block.ElementId == elementId))
            .Select(page => page.PageNumber)
            .First();

    private static IReadOnlyList<TableFragment> TableFragments(
        DocumentLayoutResult layout,
        Guid tableId) =>
        layout.Pages
            .OrderBy(page => page.PageNumber)
            .SelectMany(page => page.Blocks
                .Where(block => block.ElementId == tableId && block.Payload is TablePageBlockPayload)
                .Select(block => new TableFragment(
                    page.PageNumber,
                    block,
                    (TablePageBlockPayload)block.Payload)))
            .OrderBy(fragment => fragment.Block.FragmentIndex)
            .ToList();

    private static IReadOnlyList<string> FragmentSignature(
        DocumentLayoutResult layout,
        Guid tableId) =>
        TableFragments(layout, tableId)
            .Select(fragment => string.Join(
                ":",
                fragment.PageNumber,
                fragment.Block.FragmentIndex,
                fragment.Payload.StartRowIndex,
                fragment.Payload.Rows.Count,
                fragment.Payload.IsHeaderRepeated,
                fragment.Payload.Caption is null ? "continuation" : "first"))
            .ToList();

    private sealed record TableFragment(
        int PageNumber,
        PositionedPageBlock Block,
        TablePageBlockPayload Payload);
}
