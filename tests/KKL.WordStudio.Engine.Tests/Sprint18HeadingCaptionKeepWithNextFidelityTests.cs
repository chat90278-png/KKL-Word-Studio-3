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

        var boundaryWordCount = await FindBoundaryWordCountAsync(
            profile,
            ids,
            visibleCaption);
        var automaticLayout = await LayoutAsync(
            profile,
            ids,
            boundaryWordCount,
            description,
            sequence);

        Assert.Equal(1, LastPageFor(automaticLayout, ids.FillerId));
        Assert.Equal(2, FirstPageFor(automaticLayout, ids.HeadingId));
        Assert.Equal(2, FirstPageFor(automaticLayout, ids.TableId));

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

    private static async Task<int> FindBoundaryWordCountAsync(
        DocumentFormatProfile profile,
        FixtureIds ids,
        string visibleCaption)
    {
        for (var wordCount = 1; wordCount <= 120; wordCount++)
        {
            var layout = await LayoutAsync(
                profile,
                ids,
                wordCount,
                visibleCaption,
                sequence: null);

            if (LastPageFor(layout, ids.FillerId) == 1
                && FirstPageFor(layout, ids.HeadingId) == 2
                && FirstPageFor(layout, ids.TableId) == 2)
            {
                return wordCount;
            }
        }

        throw new Xunit.Sdk.XunitException(
            "Could not find the deterministic heading/table keep-with-next boundary within 120 filler words.");
    }

    private static async Task<DocumentLayoutResult> LayoutAsync(
        DocumentFormatProfile profile,
        FixtureIds ids,
        int fillerWordCount,
        string caption,
        TableCaptionSequenceProfile? sequence)
    {
        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes =
            [
                new TextContentNode
                {
                    ElementId = ids.FillerId,
                    Kind = ReportContentKind.Paragraph,
                    Text = string.Join(' ', Enumerable.Repeat("dolgu", fillerWordCount)),
                    Format = profile.BodyText
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
                HeightMillimeters = 90d,
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
