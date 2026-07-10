namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint15GroupedTablePaginationTests
{
    [Fact]
    public async Task GroupedRows_StayTogetherWhenTheyFitFreshPage()
    {
        var tableId = Guid.NewGuid();
        var result = await LayoutAsync(TableDocument(
            tableId,
            Rows("A", "B", "C"),
            rowGroups: [Group(0, 3)],
            pageLayout: CreatePageLayout(height: 45)));

        var fragments = TableFragments(result, tableId);

        var payload = Assert.IsType<TablePageBlockPayload>(Assert.Single(fragments).Payload);
        Assert.Equal(3, payload.Rows.Count);
        Assert.Equal(new[] { "A", "B", "C" }, payload.Rows.Select(row => row[4]));
    }

    [Fact]
    public async Task GroupedRows_MoveToNextPageWhenCurrentRemainderIsTooSmall()
    {
        var tableId = Guid.NewGuid();
        var rows = Rows("önce", "A", "B", "C");
        var result = await LayoutAsync(TableDocument(
            tableId,
            rows,
            rowGroups: [Group(1, 3)],
            pageLayout: CreatePageLayout(height: 45)));

        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.Equal(new[] { 1, 3 }, payloads.Select(payload => payload.Rows.Count));
        Assert.Equal(1, payloads[1].StartRowIndex);
        Assert.Equal(new[] { "A", "B", "C" }, payloads[1].Rows.Select(row => row[4]));
    }

    [Fact]
    public async Task OversizedGroup_SplitsAtRowBoundariesWithProgress()
    {
        var tableId = Guid.NewGuid();
        var rows = Rows("A", "B", "C", "D", "E");
        var result = await LayoutAsync(TableDocument(
            tableId,
            rows,
            rowGroups: [Group(0, 5)],
            pageLayout: CreatePageLayout(height: 45)));

        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.True(payloads.Count >= 2);
        Assert.All(payloads, payload => Assert.NotEmpty(payload.Rows));
        Assert.Equal(new[] { "A", "B", "C", "D", "E" }, payloads.SelectMany(payload => payload.Rows).Select(row => row[4]));
    }

    [Fact]
    public async Task FragmentSpans_AreLocalToPayloadRows()
    {
        var tableId = Guid.NewGuid();
        var result = await LayoutAsync(TableDocument(
            tableId,
            Rows("A", "B", "C", "D"),
            cellSpans: [Span(0, 1, 4)],
            pageLayout: CreatePageLayout(height: 38.0)));

        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.True(payloads.Count >= 2);
        Assert.All(payloads.SelectMany(payload => payload.CellSpans), span =>
        {
            var owner = payloads.Single(payload => payload.CellSpans.Contains(span));
            Assert.InRange(span.RowIndex, 0, owner.Rows.Count - 1);
            Assert.True(span.RowIndex + span.RowSpan <= owner.Rows.Count);
        });
        Assert.Equal(0, payloads[1].CellSpans.Single().RowIndex);
    }

    [Fact]
    public async Task SpanCrossingPageBoundary_RestartsAtContinuationFragment()
    {
        var tableId = Guid.NewGuid();
        var result = await LayoutAsync(TableDocument(
            tableId,
            Rows("A", "B", "C", "D"),
            cellSpans: [Span(0, 2, 4)],
            pageLayout: CreatePageLayout(height: 38.0)));

        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.Equal(0, payloads[0].CellSpans.Single().RowIndex);
        Assert.Equal(0, payloads[1].CellSpans.Single().RowIndex);
        Assert.Equal(2, payloads[1].CellSpans.Single().RowSpan);
    }

    [Fact]
    public async Task SpanContinuation_CopiesSemanticAnchorValue()
    {
        var tableId = Guid.NewGuid();
        var rows = Rows("A", "B", "C", "D")
            .Select((row, index) => WithCell(row, 1, index == 0 ? "Elma" : string.Empty))
            .ToList();
        var result = await LayoutAsync(TableDocument(
            tableId,
            rows,
            cellSpans: [Span(0, 1, 4)],
            pageLayout: CreatePageLayout(height: 35.5)));

        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.Equal("Elma", payloads[1].Rows[0][1]);
        Assert.Equal(string.Empty, rows[2][1]);
    }

    [Fact]
    public async Task SingleRowSpanIntersection_EmitsValueWithoutInvalidRowSpan()
    {
        var tableId = Guid.NewGuid();
        var rows = Rows("A", "B", "C")
            .Select((row, index) => WithCell(row, 3, index == 0 ? "321" : string.Empty))
            .ToList();
        var result = await LayoutAsync(TableDocument(
            tableId,
            rows,
            cellSpans: [Span(0, 3, 3)],
            pageLayout: CreatePageLayout(height: 35.5)));

        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();
        var finalPayload = payloads[^1];

        Assert.Single(finalPayload.Rows);
        Assert.Empty(finalPayload.CellSpans);
        Assert.Equal("321", finalPayload.Rows[0][3]);
    }

    [Fact]
    public async Task InvalidSemanticSpan_IsWarnedAndDoesNotCrash()
    {
        var tableId = Guid.NewGuid();
        var result = await LayoutAsync(TableDocument(
            tableId,
            Rows("A", "B"),
            cellSpans:
            [
                Span(-1, 1, 2),
                Span(0, 99, 2),
                Span(1, 1, 5)
            ]));

        Assert.Contains(result.Warnings, warning => warning.Contains("geçersiz hücre birleştirme aralığı", StringComparison.Ordinal));
        Assert.All(
            TableFragments(result, tableId).Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload)),
            payload => Assert.Empty(payload.CellSpans));
    }

    [Fact]
    public async Task CompositionWarnings_AppearInDocumentLayoutWarnings()
    {
        const string warning = "PN/key '1234' için Adet 3, eşleşen Seri No 2; çoklu seri düzeni uygulanmadı.";
        var tableId = Guid.NewGuid();
        var result = await LayoutAsync(TableDocument(
            tableId,
            Rows("A"),
            compositionWarnings: [warning, warning]));

        Assert.Equal(1, result.Warnings.Count(candidate => candidate == warning));
    }

    [Fact]
    public async Task GroupedTableFragments_PreserveElementIdAndRowOrder()
    {
        var tableId = Guid.NewGuid();
        var rows = Rows("A", "B", "C", "D", "E");
        var result = await LayoutAsync(TableDocument(
            tableId,
            rows,
            rowGroups: [Group(0, 5)],
            pageLayout: CreatePageLayout(height: 45)));
        var fragments = TableFragments(result, tableId);

        Assert.All(fragments, fragment => Assert.Equal(tableId, fragment.ElementId));
        Assert.Equal(
            rows.Select(row => row[4]),
            fragments.SelectMany(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload).Rows).Select(row => row[4]));
    }

    [Fact]
    public async Task RepeatedTableHeader_RemainsOnGroupedContinuationPage()
    {
        var tableId = Guid.NewGuid();
        var result = await LayoutAsync(TableDocument(
            tableId,
            Rows("A", "B", "C", "D", "E"),
            rowGroups: [Group(0, 5)],
            pageLayout: CreatePageLayout(height: 45)));
        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.True(payloads.Count >= 2);
        Assert.True(payloads[0].HasHeader);
        Assert.False(payloads[0].IsHeaderRepeated);
        Assert.All(payloads.Skip(1), payload => Assert.True(payload.IsHeaderRepeated));
    }

    [Fact]
    public async Task GroupedCaption_RemainsFirstFragmentOnly()
    {
        var tableId = Guid.NewGuid();
        var result = await LayoutAsync(TableDocument(
            tableId,
            Rows("A", "B", "C", "D", "E"),
            rowGroups: [Group(0, 5)],
            caption: "Tablo 15",
            pageLayout: CreatePageLayout(height: 52)));
        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.True(payloads.Count >= 2);
        Assert.Equal("Tablo 15", payloads[0].Caption);
        Assert.All(payloads.Skip(1), payload => Assert.Null(payload.Caption));
    }

    private static Task<DocumentLayoutResult> LayoutAsync(ReportContentDocument document) =>
        new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

    private static ReportContentDocument TableDocument(
        Guid tableId,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<TableCellSpan>? cellSpans = null,
        IReadOnlyList<TableRowGroup>? rowGroups = null,
        IReadOnlyList<string>? compositionWarnings = null,
        string? caption = null,
        PageLayout? pageLayout = null) =>
        new()
        {
            HeaderNodes = [],
            BodyNodes =
            [
                new TableContentNode
                {
                    ElementId = tableId,
                    Kind = ReportContentKind.Table,
                    Name = "Grouped Table",
                    Caption = caption,
                    ColumnHeaders = ["No", "Product Name", "Product No", "NSN", "Serial No", "Quantity"],
                    Rows = rows,
                    CellSpans = cellSpans ?? [],
                    RowGroups = rowGroups ?? [],
                    CompositionWarnings = compositionWarnings ?? [],
                    DataSourceName = null,
                    SourceCount = 0,
                    SourceError = null,
                    FilterWasIgnored = false
                }
            ],
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = pageLayout ?? CreatePageLayout()
        };

    private static IReadOnlyList<PositionedPageBlock> TableFragments(DocumentLayoutResult result, Guid tableId) =>
        result.Pages
            .SelectMany(page => page.Blocks)
            .Where(block => block.ElementId == tableId && block.Kind == PageBlockKind.Table)
            .ToList();

    private static IReadOnlyList<IReadOnlyList<string>> Rows(params string[] serials) =>
        serials.Select((serial, index) => (IReadOnlyList<string>)new[]
        {
            index == 0 ? "1" : string.Empty,
            index == 0 ? "Elma" : string.Empty,
            index == 0 ? "1234" : string.Empty,
            index == 0 ? "321" : string.Empty,
            serial,
            index == 0 ? serials.Length.ToString() : string.Empty
        }).ToList();

    private static IReadOnlyList<string> WithCell(IReadOnlyList<string> row, int columnIndex, string value)
    {
        var copy = row.ToList();
        copy[columnIndex] = value;
        return copy;
    }

    private static TableCellSpan Span(int rowIndex, int columnIndex, int rowSpan) =>
        new() { RowIndex = rowIndex, ColumnIndex = columnIndex, RowSpan = rowSpan };

    private static TableRowGroup Group(int startRowIndex, int rowCount) =>
        new() { StartRowIndex = startRowIndex, RowCount = rowCount, KeepTogetherWhenPossible = true };

    private static PageLayout CreatePageLayout(
        double width = 120,
        double height = 80,
        double top = 5,
        double bottom = 5,
        double left = 10,
        double right = 10) =>
        new()
        {
            WidthMillimeters = width,
            HeightMillimeters = height,
            MarginTopMillimeters = top,
            MarginBottomMillimeters = bottom,
            MarginLeftMillimeters = left,
            MarginRightMillimeters = right,
            ShowPageNumbers = false
        };
}
