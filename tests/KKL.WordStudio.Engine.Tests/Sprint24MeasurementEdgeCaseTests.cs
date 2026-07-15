namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint24MeasurementEdgeCaseTests
{
    [Fact]
    public async Task CompatibilityFallback_MatchesEquivalentPhysicalCellInsetsAtPageBoundaries()
    {
        var rows = Rows(18, i => [$"ROW-{i:00}", $"Uzun bakım açıklaması {i} periyodik kontrol bağlantı elemanı değişim kaydı"]);
        var implicitFallback = await LayoutAsync(Format([]), rows, 86d);
        var explicitEquivalent = await LayoutAsync(
            Format(FallbackColumns(2), verticalMargin: 1.25d, horizontalMargin: 1.5d),
            rows,
            86d);

        Assert.Equal(Signature(implicitFallback), Signature(explicitEquivalent));
    }

    [Fact]
    public async Task RealCellMargins_IncreaseMeasuredPreviewTableHeight()
    {
        var rows = Rows(8, i => [i.ToString(), $"Satır yüksekliği ölçüm senaryosu {i} bakım açıklaması"]);
        var compatibility = await LayoutAsync(
            Format(FallbackColumns(2), verticalMargin: 1.25d, horizontalMargin: 1.5d),
            rows,
            297d);
        var padded = await LayoutAsync(
            Format(FallbackColumns(2), verticalMargin: 2d, horizontalMargin: 1.5d),
            rows,
            297d);

        var compatibilityBlock = Assert.Single(Fragments(compatibility)).Block;
        var paddedBlock = Assert.Single(Fragments(padded)).Block;
        Assert.True(
            paddedBlock.HeightMillimeters > compatibilityBlock.HeightMillimeters,
            $"Compatibility height: {compatibilityBlock.HeightMillimeters}, padded height: {paddedBlock.HeightMillimeters}");
    }

    [Fact]
    public async Task NoWrapColumn_LongTokenDoesNotCreateExtraPreviewFragments()
    {
        var shortRows = Rows(24, i => [$"PN-{i:000}"]);
        var longRows = Rows(24, i => [$"PN-{i:000}-ABCDEFGHIJKLMNOPQRSTUVWXYZ-1234567890-ABCDEFGHIJKLMNOPQRSTUVWXYZ"]);
        var format = Format(FallbackColumns(1, noWrap: true));

        Assert.Equal(
            Signature(await LayoutAsync(format, shortRows, 78d)),
            Signature(await LayoutAsync(format, longRows, 78d)));
    }

    private static Task<DocumentLayoutResult> LayoutAsync(
        ResolvedTableFormat format,
        IReadOnlyList<IReadOnlyList<string>> rows,
        double pageHeight) =>
        new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = new ReportContentDocument
            {
                HeaderNodes = [],
                BodyNodes =
                [
                    new TableContentNode
                    {
                        ElementId = TableId,
                        Kind = ReportContentKind.Table,
                        Name = "Measurement table",
                        Caption = "Measurement table",
                        ColumnHeaders = Enumerable.Range(1, rows.Max(r => r.Count)).Select(i => $"Column {i}").ToArray(),
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
                    HeightMillimeters = pageHeight,
                    MarginTopMillimeters = 10d,
                    MarginBottomMillimeters = 10d,
                    MarginLeftMillimeters = 15d,
                    MarginRightMillimeters = 15d,
                    ShowPageNumbers = false
                }
            },
            FrontMatter = null
        });

    private static ResolvedTableFormat Format(
        IReadOnlyList<ResolvedTableColumnFormat> columns,
        double verticalMargin = 0d,
        double horizontalMargin = 0d) => new()
    {
        WidthPercent = 100d,
        FixedLayout = true,
        BorderSizePoints = 0.5d,
        CellMarginTopMillimeters = verticalMargin,
        CellMarginBottomMillimeters = verticalMargin,
        CellMarginLeftMillimeters = horizontalMargin,
        CellMarginRightMillimeters = horizontalMargin,
        PreferredRowHeightMillimeters = 0d,
        RepeatHeader = true,
        Columns = columns
    };

    private static IReadOnlyList<ResolvedTableColumnFormat> FallbackColumns(int count, bool noWrap = false) =>
        Enumerable.Range(0, count).Select(_ => new ResolvedTableColumnFormat
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
        }).ToArray();

    private static IReadOnlyList<IReadOnlyList<string>> Rows(
        int count,
        Func<int, IReadOnlyList<string>> factory) =>
        Enumerable.Range(1, count).Select(factory).ToArray();

    private static IReadOnlyList<string> Signature(DocumentLayoutResult layout) =>
        Fragments(layout).Select(f => string.Join(
            ":",
            f.PageNumber,
            f.Block.FragmentIndex,
            f.Payload.StartRowIndex,
            f.Payload.Rows.Count,
            f.Payload.IsHeaderRepeated,
            f.Payload.Caption is null ? "continuation" : "first")).ToArray();

    private static IReadOnlyList<TableFragment> Fragments(DocumentLayoutResult layout) =>
        layout.Pages.OrderBy(p => p.PageNumber)
            .SelectMany(p => p.Blocks
                .Where(b => b.ElementId == TableId && b.Payload is TablePageBlockPayload)
                .Select(b => new TableFragment(p.PageNumber, b, (TablePageBlockPayload)b.Payload)))
            .OrderBy(f => f.Block.FragmentIndex)
            .ToArray();

    private static readonly Guid TableId = Guid.Parse("80C9010E-CE8C-4B13-A854-1D350B4E8A10");
    private sealed record TableFragment(int PageNumber, PositionedPageBlock Block, TablePageBlockPayload Payload);
}
