namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint24HeadingChainPaginationTests
{
    [Fact]
    public void PageBreakBeforeFollowingTable_CarriesWholeTrailingHeadingChain()
    {
        var headingId = Guid.NewGuid();
        var altHeadingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var flow = CreateFlow();

        flow.AddBodyBlock(TextBlock(Guid.NewGuid(), ReportContentKind.Paragraph, flow.BodyYMillimeters, 180d));
        flow.AddBodyBlock(TextBlock(headingId, ReportContentKind.Heading, flow.BodyYMillimeters, 8d));
        flow.AddBodyBlock(TextBlock(altHeadingId, ReportContentKind.AltHeading, flow.BodyYMillimeters, 8d));

        // This is the same boundary requested by the table paginator when its
        // caption/header/first-row minimum cannot fit in the remaining space.
        flow.NewPage();
        flow.AddBodyBlock(TableBlock(tableId, flow.BodyYMillimeters, 30d));

        var pages = flow.Complete();

        Assert.Equal(2, pages.Count);
        Assert.DoesNotContain(pages[0].Blocks, block =>
            block.ElementId == headingId || block.ElementId == altHeadingId);
        Assert.Collection(
            pages[1].Blocks.Where(block => block.Region == DocumentPageRegion.Body),
            block => Assert.Equal(headingId, block.ElementId),
            block => Assert.Equal(altHeadingId, block.ElementId),
            block => Assert.Equal(tableId, block.ElementId));
    }

    [Fact]
    public void HeadingOnlyFreshPage_IsNotMovedAgainAndDoesNotCreateEmptyPage()
    {
        var headingId = Guid.NewGuid();
        var flow = CreateFlow();
        flow.AddBodyBlock(TextBlock(headingId, ReportContentKind.Heading, flow.BodyYMillimeters, 8d));

        flow.NewPage();
        flow.AddBodyBlock(TableBlock(Guid.NewGuid(), flow.BodyYMillimeters, 30d));

        var pages = flow.Complete();

        Assert.Equal(2, pages.Count);
        Assert.Contains(pages[0].Blocks, block => block.ElementId == headingId);
        Assert.NotEmpty(pages[0].Blocks.Where(block => block.Region == DocumentPageRegion.Body));
        Assert.NotEmpty(pages[1].Blocks.Where(block => block.Region == DocumentPageRegion.Body));
    }

    private static LayoutPageFlow CreateFlow() => new(
        firstPageNumber: 1,
        DocumentPageOrigin.GeneratedReport,
        new PageLayout
        {
            WidthMillimeters = 210d,
            HeightMillimeters = 297d,
            MarginTopMillimeters = 20d,
            MarginBottomMillimeters = 20d,
            MarginLeftMillimeters = 20d,
            MarginRightMillimeters = 20d,
            ShowPageNumbers = false
        });

    private static PositionedPageBlock TextBlock(
        Guid id,
        ReportContentKind kind,
        double y,
        double height) => new()
    {
        ElementId = id,
        Region = DocumentPageRegion.Body,
        Kind = PageBlockKind.Text,
        XMillimeters = 20d,
        YMillimeters = y,
        WidthMillimeters = 170d,
        HeightMillimeters = height,
        FragmentIndex = 0,
        IsContinuation = false,
        IsEditableReportElement = true,
        Payload = new TextPageBlockPayload
        {
            Runs = [],
            SemanticKind = kind,
            Alignment = ParagraphAlignment.Left,
            Format = DefaultFormatProfiles.BodyText
        }
    };

    private static PositionedPageBlock TableBlock(Guid id, double y, double height) => new()
    {
        ElementId = id,
        Region = DocumentPageRegion.Body,
        Kind = PageBlockKind.Table,
        XMillimeters = 20d,
        YMillimeters = y,
        WidthMillimeters = 170d,
        HeightMillimeters = height,
        FragmentIndex = 0,
        IsContinuation = false,
        IsEditableReportElement = true,
        Payload = new TablePageBlockPayload
        {
            Name = "Table",
            ColumnHeaders = ["No"],
            Rows = [["1"]],
            CellSpans = [],
            StartRowIndex = 0,
            HasHeader = true,
            IsHeaderRepeated = false,
            Format = DefaultFormatProfiles.Table
        }
    };
}
