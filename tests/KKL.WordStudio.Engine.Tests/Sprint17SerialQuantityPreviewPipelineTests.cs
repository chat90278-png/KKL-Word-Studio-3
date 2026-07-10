namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint17SerialQuantityPreviewPipelineTests
{
    [Fact]
    public async Task UnitQuantityPhysicalSerialRows_ReachPreviewPayloadAsTrueRowsAndSpans()
    {
        var table = CreateConfiguredTable(
            "No",
            "Tr İsim",
            "Parça Numarası",
            "NSN",
            "Seri Numarası",
            "Adet");
        var composition = new SerialQuantityTableContentRowComposer().Compose(table,
        [
            ["1", "elma", "1234", "45-50-60", "9999", "1"],
            ["2", "armut", "56789", "459-485-5", "9987", "1"],
            ["", "armut", "56789", "", "9988", "1"]
        ]);

        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes =
            [
                new TableContentNode
                {
                    ElementId = table.Id,
                    Kind = ReportContentKind.Table,
                    Name = "Real serial source shape",
                    Caption = null,
                    ColumnHeaders = table.Columns.Select(column => column.Header).ToArray(),
                    Rows = composition.Rows,
                    CellSpans = composition.CellSpans,
                    RowGroups = composition.RowGroups,
                    CompositionWarnings = composition.Warnings,
                    DataSourceName = null,
                    SourceCount = 0,
                    SourceError = null,
                    FilterWasIgnored = false
                }
            ],
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = new PageLayout
            {
                WidthMillimeters = 210,
                HeightMillimeters = 297,
                MarginTopMillimeters = 25,
                MarginBottomMillimeters = 25,
                MarginLeftMillimeters = 25,
                MarginRightMillimeters = 25,
                ShowPageNumbers = false
            }
        };

        var layout = await new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

        var block = Assert.Single(layout.Pages
            .SelectMany(page => page.Blocks)
            .Where(candidate => candidate.ElementId == table.Id && candidate.Kind == PageBlockKind.Table));
        var payload = Assert.IsType<TablePageBlockPayload>(block.Payload);

        Assert.Equal(3, payload.Rows.Count);
        Assert.Equal("9999", payload.Rows[0][4]);
        Assert.Equal("9987", payload.Rows[1][4]);
        Assert.Equal("9988", payload.Rows[2][4]);
        Assert.Equal("2", payload.Rows[1][5]);
        Assert.Equal(string.Empty, payload.Rows[2][5]);

        Assert.Equal(5, payload.CellSpans.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 5 }, payload.CellSpans.Select(span => span.ColumnIndex).OrderBy(index => index));
        Assert.All(payload.CellSpans, span =>
        {
            Assert.Equal(1, span.RowIndex);
            Assert.Equal(2, span.RowSpan);
        });
        Assert.DoesNotContain(payload.CellSpans, span => span.ColumnIndex == 4);
        Assert.Empty(layout.Warnings);
    }

    private static TableElement CreateConfiguredTable(params string[] headers)
    {
        var table = new TableElement { Name = "Sprint17 preview pipeline table" };
        foreach (var header in headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });

        table.SerialQuantityGrouping = new SerialQuantityGroupingDetector().Detect(table.Columns);
        Assert.NotNull(table.SerialQuantityGrouping);
        return table;
    }
}
