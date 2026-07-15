namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint15ContractBootstrapTests
{
    [Fact]
    public async Task FallbackLayout_PreservesTableCellSpans()
    {
        IReadOnlyList<TableCellSpan> spans =
        [
            new TableCellSpan { RowIndex = 0, ColumnIndex = 0, RowSpan = 2 }
        ];
        var table = new TableContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.Table,
            Name = "Grouped table",
            ColumnHeaders = ["PN", "Seri No"],
            Rows =
            [
                new[] { "PN-1", "SN-1" },
                new[] { "", "SN-2" }
            ],
            CellSpans = spans
        };
        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes = [table],
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = new PageLayout
            {
                WidthMillimeters = 210,
                HeightMillimeters = 297,
                MarginTopMillimeters = 20,
                MarginBottomMillimeters = 20,
                MarginLeftMillimeters = 20,
                MarginRightMillimeters = 20,
                ShowPageNumbers = false
            }
        };
        var engine = new DeterministicDocumentLayoutEngine();

        var result = await engine.LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

        var block = Assert.Single(Assert.Single(result.Pages).Blocks);
        var payload = Assert.IsType<TablePageBlockPayload>(block.Payload);
        var span = Assert.Single(payload.CellSpans);
        Assert.Equal(0, span.RowIndex);
        Assert.Equal(0, span.ColumnIndex);
        Assert.Equal(2, span.RowSpan);
    }
}
