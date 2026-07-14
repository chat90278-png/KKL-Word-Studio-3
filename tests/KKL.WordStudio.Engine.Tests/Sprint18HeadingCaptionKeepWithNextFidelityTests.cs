namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint18HeadingCaptionKeepWithNextFidelityTests
{
    [Fact]
    public async Task AutomaticNumberedCaption_MatchesEquivalentVisibleCaptionAtHeadingPageBoundary()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var sequence = profile.TableCaptionSequence!;
        const string description = "Uzun tablo başlığı sınır ölçüm testi";
        var visibleCaption = TableCaptionSequenceFormatter.BuildDisplayText(description, sequence, 1);
        var ids = new FixtureIds(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var visibleLayout = await LayoutAsync(
            profile,
            ids,
            visibleCaption,
            sequence: null);
        var automaticLayout = await LayoutAsync(
            profile,
            ids,
            description,
            sequence);

        var visibleFillerLastPage = LastPageFor(visibleLayout, ids.FillerId);
        var automaticFillerLastPage = LastPageFor(automaticLayout, ids.FillerId);
        var visibleHeadingPage = FirstPageFor(visibleLayout, ids.HeadingId);
        var automaticHeadingPage = FirstPageFor(automaticLayout, ids.HeadingId);
        var visibleTablePage = FirstPageFor(visibleLayout, ids.TableId);
        var automaticTablePage = FirstPageFor(automaticLayout, ids.TableId);

        Assert.Equal(visibleFillerLastPage, automaticFillerLastPage);
        Assert.Equal(visibleHeadingPage, automaticHeadingPage);
        Assert.Equal(visibleTablePage, automaticTablePage);
        Assert.True(visibleHeadingPage > visibleFillerLastPage);
        Assert.Equal(visibleHeadingPage, visibleTablePage);

        var tablePayload = automaticLayout.Pages
            .SelectMany(page => page.Blocks)
            .Where(block => block.ElementId == ids.TableId && block.FragmentIndex == 0)
            .Select(block => Assert.IsType<TablePageBlockPayload>(block.Payload))
            .Single();
        Assert.Equal(description, tablePayload.Caption);
        Assert.Same(sequence, tablePayload.CaptionSequence);
        Assert.Equal(1, tablePayload.CaptionSequenceNumber);
        Assert.Equal(
            visibleCaption,
            TableCaptionSequenceFormatter.BuildDisplayText(
                tablePayload.Caption!,
                tablePayload.CaptionSequence,
                tablePayload.CaptionSequenceNumber));
    }

    private static async Task<DocumentLayoutResult> LayoutAsync(
        DocumentFormatProfile profile,
        FixtureIds ids,
        string caption,
        TableCaptionSequenceProfile? sequence)
    {
        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes =
            [
                new ImageContentNode
                {
                    ElementId = ids.FillerId,
                    Kind = ReportContentKind.Image,
                    Name = "Fixed forty millimeter boundary filler"
                },
                new TextContentNode
                {
                    ElementId = ids.HeadingId,
                    Kind = ReportContentKind.Heading,
                    Text = "Keep-with-next boundary heading",
                    Format = profile.PrimaryHeading
                },
                new TableContentNode
                {
                    ElementId = ids.TableId,
                    Kind = ReportContentKind.Table,
                    Name = "Boundary table",
                    Caption = caption,
                    CaptionFormat = profile.TableCaption,
                    CaptionSequence = sequence,
                    ColumnHeaders = ["No", "Product Name", "Product Number", "NSN", "Serial No", "Quantity"],
                    Rows = [["1", "Product", "P-001", "NSN-001", "SER-001", "1"]],
                    Format = profile.TableFormats[0].Format,
                    SourceCount = 0
                }
            ],
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = new PageLayout
            {
                WidthMillimeters = 80d,
                HeightMillimeters = 128d,
                MarginTopMillimeters = 10d,
                MarginBottomMillimeters = 10d,
                MarginLeftMillimeters = 10d,
                MarginRightMillimeters = 10d,
                HeaderDistanceMillimeters = 5d,
                FooterDistanceMillimeters = 5d,
                ShowPageNumbers = false
            }
        };

        return await new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });
    }

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

    private sealed record FixtureIds(Guid FillerId, Guid HeadingId, Guid TableId);
}
