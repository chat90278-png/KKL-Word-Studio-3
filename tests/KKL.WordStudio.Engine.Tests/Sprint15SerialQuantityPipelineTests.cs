namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint15SerialQuantityPipelineTests
{
    private static readonly IReadOnlyList<string> Headers =
        ["No", "Product Name", "Product No", "NSN", "Serial No", "Quantity"];

    [Fact]
    public async Task SerialQuantityPipeline_ExactTwoSerials_ComposerToEnginePreservesGroupedSpans()
    {
        var table = CreateConfiguredTable();
        var composition = new SerialQuantityTableContentRowComposer().Compose(
            table,
            [
                ["1", "Elma", "1234", "321", "A222", "2"],
                ["2", "Elma", "1234", "321", "A221", "2"]
            ]);

        Assert.Equal(2, composition.Rows.Count);
        Assert.Equal(new[] { "A222", "A221" }, composition.Rows.Select(row => row[4]));
        var rowGroup = Assert.Single(composition.RowGroups);
        Assert.Equal(2, rowGroup.RowCount);
        Assert.True(rowGroup.KeepTogetherWhenPossible);
        Assert.Contains(composition.CellSpans, span => span is { ColumnIndex: 0, RowSpan: 2 });
        Assert.Contains(composition.CellSpans, span => span is { ColumnIndex: 1, RowSpan: 2 });
        Assert.Contains(composition.CellSpans, span => span is { ColumnIndex: 2, RowSpan: 2 });
        Assert.Contains(composition.CellSpans, span => span is { ColumnIndex: 3, RowSpan: 2 });
        Assert.Contains(composition.CellSpans, span => span is { ColumnIndex: 5, RowSpan: 2 });
        Assert.DoesNotContain(composition.CellSpans, span => span.ColumnIndex == 4);

        var result = await LayoutAsync(CreateDocument(table.Id, composition));
        var fragments = TableFragments(result, table.Id);
        Assert.NotEmpty(fragments);
        Assert.All(fragments, fragment => Assert.Equal(table.Id, fragment.ElementId));

        var payloads = fragments
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();
        Assert.Equal(new[] { "A222", "A221" }, payloads.SelectMany(payload => payload.Rows).Select(row => row[4]));
        Assert.DoesNotContain(payloads.SelectMany(payload => payload.CellSpans), span => span.ColumnIndex == 4);
        Assert.All(payloads, payload =>
            Assert.All(payload.CellSpans, span =>
            {
                Assert.InRange(span.RowIndex, 0, payload.Rows.Count - 1);
                Assert.True(span.RowIndex + span.RowSpan <= payload.Rows.Count);
                Assert.True(span.RowSpan >= 2);
            }));
    }

    [Fact]
    public async Task SerialQuantityPipeline_Quantity100WithoutSerial_RemainsOneSemanticAndLayoutRow()
    {
        var table = CreateConfiguredTable();
        var composition = new SerialQuantityTableContentRowComposer().Compose(
            table,
            [
                ["1", "Elma", "1234", "321", "", "100"]
            ]);

        Assert.Single(composition.Rows);
        Assert.Empty(composition.CellSpans);
        Assert.Empty(composition.RowGroups);

        var result = await LayoutAsync(CreateDocument(table.Id, composition));
        var layoutRowCount = TableFragments(result, table.Id)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .Sum(payload => payload.Rows.Count);

        Assert.Equal(1, layoutRowCount);
    }

    private static TableElement CreateConfiguredTable()
    {
        var table = new TableElement { Name = "Sprint 15 Pipeline Table" };
        foreach (var header in Headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });

        table.SerialQuantityGrouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = table.Columns[2].Id,
            SerialNumberColumnId = table.Columns[4].Id,
            QuantityColumnId = table.Columns[5].Id,
            WasAutoDetected = false
        };
        return table;
    }

    private static ReportContentDocument CreateDocument(
        Guid tableId,
        TableRowCompositionResult composition) =>
        new()
        {
            HeaderNodes = [],
            BodyNodes =
            [
                new TableContentNode
                {
                    ElementId = tableId,
                    Kind = ReportContentKind.Table,
                    Name = "Sprint 15 Pipeline Table",
                    Caption = null,
                    ColumnHeaders = Headers,
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
                WidthMillimeters = 120,
                HeightMillimeters = 80,
                MarginTopMillimeters = 5,
                MarginBottomMillimeters = 5,
                MarginLeftMillimeters = 10,
                MarginRightMillimeters = 10,
                ShowPageNumbers = false
            }
        };

    private static Task<DocumentLayoutResult> LayoutAsync(ReportContentDocument document) =>
        new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

    private static IReadOnlyList<PositionedPageBlock> TableFragments(DocumentLayoutResult result, Guid tableId) =>
        result.Pages
            .SelectMany(page => page.Blocks)
            .Where(block => block.ElementId == tableId && block.Kind == PageBlockKind.Table)
            .ToList();
}
