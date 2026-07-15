namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint24MeasurementEdgeCaseTests
{
    [Fact]
    public async Task EmptyColumnProfile_MatchesEquivalentWordFallbackProfileAtPageBoundaries()
    {
        var rows = CreateRows(
            rowCount: 18,
            valueFactory: index =>
                (IReadOnlyList<string>)[
                    $"ROW-{index:00}",
                    $"Uzun bakım açıklaması {index} periyodik kontrol bağlantı elemanı değişim kaydı"]);

        var implicitFallback = await LayoutAsync(
            CreateFormat(columns: []),
            rows,
            pageHeightMillimeters: 86d);
        var explicitFallback = await LayoutAsync(
            CreateFormat(columns: CreateFallbackColumns(2)),
            rows,
            pageHeightMillimeters: 86d);

        Assert.Equal(FragmentSignature(implicitFallback), FragmentSignature(explicitFallback));
    }

    [Fact]
    public async Task RealCellMargins_ReduceRowsThatFitOnTheFirstPreviewPage()
    {
        var rows = CreateRows(
            rowCount: 20,
            valueFactory: index =>
                (IReadOnlyList<string>)[
                    index.ToString(),
                    $"Dar sayfada satır yüksekliği ölçüm senaryosu {index} bakım açıklaması"]);

        var zeroMargins = await LayoutAsync(
            CreateFormat(columns: [], verticalMarginMillimeters: 0d),
            rows,
            pageHeightMillimeters: 80d);
        var padded = await LayoutAsync(
            CreateFormat(columns: [], verticalMarginMillimeters: 2d),
            rows,
            pageHeightMillimeters: 80d);

        Assert.True(
            FirstFragmentRowCount(zeroMargins) > FirstFragmentRowCount(padded),
            $"Zero-margin first fragment: {FirstFragmentRowCount(zeroMargins)}, padded first fragment: {FirstFragmentRowCount(padded)}");
    }

    [Fact]
    public async Task NoWrapColumn_LongTokenDoesNotCreateExtraPreviewFragments()
    {
        var shortRows = CreateRows(
            rowCount: 24,
            valueFactory: index => (IReadOnlyList<string>)[$"PN-{index:000}"]);
        var longRows = CreateRows(
            rowCount: 24,
            valueFactory: index =>
                (IReadOnlyList<string>)[$"PN-{index:000}-ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890-ABCDEFGHIJKLMNOPQRSTUVWXYZ"]);
        var format = CreateFormat(
            columns: CreateFallbackColumns(1, noWrap: true));

        var shortLayout = await LayoutAsync(format, shortRows, pageHeightMillimeters: 78d);
        var longLayout = await LayoutAsync(format, longRows, pageHeightMillimeters: 78d);

        Assert.Equal(FragmentSignature(shortLayout), FragmentSignature(longLayout));
    }

    private static async Task<DocumentLayoutResult> LayoutAsync(
        ResolvedTableFormat format,
        IReadOnlyList<IReadOnlyList<string>> rows,
        double pageHeightMillimeters)
    {
        var tableId = MeasurementTableId;
        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes =
            [
                new TableContentNode
                {
                    ElementId = tableId,
                    Kind = ReportContentKind.Table,
                    Name = "Measurement table",
                    Caption = "Measurement table",
                    ColumnHeaders = Enumerable.Range(1, rows.Max(row => row.Count))
                        .Select(index => $"Column {index}")
                        .ToArray(),
                    Rows = rows,
                    CellSpans = [],
                    RowGroups = [],
                    SourceCount = 0,
                    Format = format
                }
            ],
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = new PageLayout
            {
                WidthMillimeters = 150d,
                HeightMillimeters = pageHeightMillimeters,
                MarginTopMillimeters = 10d,
                MarginBottomMillimeters = 10d,
                MarginLeftMillimeters = 15d,
                MarginRightMillimeters = 15d,
                ShowPageNumbers = false
            }
        };

        return await new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });
    }

    private static ResolvedTableFormat CreateFormat(
        IReadOnlyList<ResolvedTableColumnFormat> columns,
        double verticalMarginMillimeters = 0d) => new()
    {
        WidthPercent = 100d,
        FixedLayout = true,
        BorderSizePoints = 0.5d,
        CellMarginTopMillimeters = verticalMarginMillimeters,
        CellMarginBottomMillimeters = verticalMarginMillimeters,
        CellMarginLeftMillimeters = 0d,
        CellMarginRightMillimeters = 0d,
        PreferredRowHeightMillimeters = 0d,
        RepeatHeader = true,
        Columns = columns
    };

    private static IReadOnlyList<ResolvedTableColumnFormat> CreateFallbackColumns(
        int count,
        bool noWrap = false) =>
        Enumerable.Range(0, count)
            .Select(_ => new ResolvedTableColumnFormat
            {
                WidthWeight = 1d,
                HeaderAlignment = ParagraphAlignment.Left,
                BodyAlignment = ParagraphAlignment.Left,
                HeaderFontFamilyName = "Segoe UI",
                HeaderFontSizePoints = 10d,
                HeaderBold = true,
                BodyFontFamilyName = "Segoe UI",
                BodyFontSizePoints = 10d,
                BodyBold = false,
                VerticalAlignment = VerticalContentAlignment.Top,
                NoWrap = noWrap
            })
            .ToArray();

    private static IReadOnlyList<IReadOnlyList<string>> CreateRows(
        int rowCount,
        Func<int, IReadOnlyList<string>> valueFactory) =>
        Enumerable.Range(1, rowCount)
            .Select(valueFactory)
            .ToArray();

    private static int FirstFragmentRowCount(DocumentLayoutResult layout) =>
        TableFragments(layout).First().Payload.Rows.Count;

    private static IReadOnlyList<string> FragmentSignature(DocumentLayoutResult layout) =>
        TableFragments(layout)
            .Select(fragment => string.Join(
                ":",
                fragment.PageNumber,
                fragment.Block.FragmentIndex,
                fragment.Payload.StartRowIndex,
                fragment.Payload.Rows.Count,
                fragment.Payload.IsHeaderRepeated,
                fragment.Payload.Caption is null ? "continuation" : "first"))
            .ToArray();

    private static IReadOnlyList<TableFragment> TableFragments(DocumentLayoutResult layout) =>
        layout.Pages
            .OrderBy(page => page.PageNumber)
            .SelectMany(page => page.Blocks
                .Where(block => block.ElementId == MeasurementTableId && block.Payload is TablePageBlockPayload)
                .Select(block => new TableFragment(
                    page.PageNumber,
                    block,
                    (TablePageBlockPayload)block.Payload)))
            .OrderBy(fragment => fragment.Block.FragmentIndex)
            .ToArray();

    private static readonly Guid MeasurementTableId = Guid.Parse("80C9010E-CE8C-4B13-A854-1D350B4E8A10");

    private sealed record TableFragment(
        int PageNumber,
        PositionedPageBlock Block,
        TablePageBlockPayload Payload);
}
