namespace KKL.WordStudio.Engine.Tests;

using Xunit;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Engine.Layout;

public sealed class FallbackDocumentLayoutEngineTests
{
    [Fact]
    public async Task FallbackLayout_ReturnsContractCompliantGeneratedPage()
    {
        var document = CreateDocument(
            bodyNodes:
            [
                new TextContentNode
                {
                    ElementId = Guid.NewGuid(),
                    Kind = ReportContentKind.Paragraph,
                    Text = "Fallback content",
                    FontSize = 11
                }
            ]);

        var engine = new FallbackDocumentLayoutEngine();
        var result = await engine.LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

        var page = Assert.Single(result.Pages);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(DocumentPageOrigin.GeneratedReport, page.Origin);
        Assert.Same(document.PageLayout, page.PageLayout);
        Assert.Contains(FallbackDocumentLayoutEngine.FallbackWarning, result.Warnings);

        var block = Assert.Single(page.Blocks);
        Assert.Equal(DocumentPageRegion.Body, block.Region);
        Assert.Equal(PageBlockKind.Text, block.Kind);
        Assert.True(block.WidthMillimeters > 0);
        Assert.True(block.HeightMillimeters > 0);
        Assert.Equal(0, block.FragmentIndex);
        Assert.False(block.IsContinuation);
        Assert.IsType<TextPageBlockPayload>(block.Payload);
    }

    [Fact]
    public async Task GeneratedFallbackBlocks_PreserveElementIdAndEditableIdentity()
    {
        var textId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var document = CreateDocument(
            bodyNodes:
            [
                new TextContentNode
                {
                    ElementId = textId,
                    Kind = ReportContentKind.Heading,
                    Text = "Heading",
                    Bold = true,
                    FontSize = 14
                },
                new TableContentNode
                {
                    ElementId = tableId,
                    Kind = ReportContentKind.Table,
                    Name = "Table",
                    ColumnHeaders = ["A"],
                    Rows = [new[] { "1" }]
                }
            ]);

        var engine = new FallbackDocumentLayoutEngine();
        var result = await engine.LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

        var generatedBlocks = Assert.Single(result.Pages).Blocks
            .Where(block => block.ElementId is not null)
            .ToList();

        Assert.Collection(
            generatedBlocks,
            text =>
            {
                Assert.Equal(textId, text.ElementId);
                Assert.True(text.IsEditableReportElement);
            },
            table =>
            {
                Assert.Equal(tableId, table.ElementId);
                Assert.True(table.IsEditableReportElement);
            });
    }

    [Fact]
    public void PreviewSnapshot_KeepsCompatibilityPropertiesAndLayout()
    {
        var layout = new DocumentLayoutResult
        {
            Pages = [],
            Warnings = []
        };
        var pageLayout = CreatePageLayout();

        var snapshot = new PreviewSnapshot
        {
            HeaderBlocks = [],
            BodyBlocks = [],
            FooterBlocks = [],
            TableOfContents = [],
            PageLayout = pageLayout,
            Layout = layout
        };

        Assert.Empty(snapshot.HeaderBlocks);
        Assert.Empty(snapshot.BodyBlocks);
        Assert.Empty(snapshot.FooterBlocks);
        Assert.Empty(snapshot.TableOfContents);
        Assert.Same(pageLayout, snapshot.PageLayout);
        Assert.Same(layout, snapshot.Layout);
    }

    private static ReportContentDocument CreateDocument(
        IReadOnlyList<ReportContentNode>? headerNodes = null,
        IReadOnlyList<ReportContentNode>? bodyNodes = null,
        IReadOnlyList<ReportContentNode>? footerNodes = null)
    {
        return new ReportContentDocument
        {
            HeaderNodes = headerNodes ?? [],
            BodyNodes = bodyNodes ?? [],
            FooterNodes = footerNodes ?? [],
            TableOfContents = [],
            PageLayout = CreatePageLayout()
        };
    }

    private static PageLayout CreatePageLayout() => new()
    {
        WidthMillimeters = 210,
        HeightMillimeters = 297,
        MarginTopMillimeters = 20,
        MarginBottomMillimeters = 20,
        MarginLeftMillimeters = 20,
        MarginRightMillimeters = 20,
        ShowPageNumbers = false
    };
}
