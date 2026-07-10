namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint16FormatAwareLayoutTests
{
    [Fact]
    public async Task ReferenceTextSpacing_AffectsPageFlow()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var plain = await LayoutAsync(Document(
        [
            Text(firstId, "Birinci paragraf", TextFormat()),
            Text(secondId, "İkinci paragraf", TextFormat())
        ]));
        var spaced = await LayoutAsync(Document(
        [
            Text(firstId, "Birinci paragraf", TextFormat(spaceAfterPoints: 18d)),
            Text(secondId, "İkinci paragraf", TextFormat())
        ]));

        var plainSecond = Block(plain, secondId);
        var spacedSecond = Block(spaced, secondId);

        Assert.True(spacedSecond.YMillimeters > plainSecond.YMillimeters + 6d);
    }

    [Fact]
    public async Task ReferenceIndent_ReducesTextLineWidth()
    {
        var text = string.Join(' ', Enumerable.Repeat("referans biçimli uzun paragraf", 55));
        var plainId = Guid.NewGuid();
        var indentedId = Guid.NewGuid();
        var plain = await LayoutAsync(Document(
            [Text(plainId, text, TextFormat())],
            Page(width: 100d, height: 297d, left: 10d, right: 10d)));
        var indented = await LayoutAsync(Document(
            [Text(indentedId, text, TextFormat(leftIndentMillimeters: 25d, firstLineIndentMillimeters: 8d))],
            Page(width: 100d, height: 297d, left: 10d, right: 10d)));

        var plainHeight = Blocks(plain, plainId).Sum(block => block.HeightMillimeters);
        var indentedBlocks = Blocks(indented, indentedId);
        var indentedHeight = indentedBlocks.Sum(block => block.HeightMillimeters);

        Assert.True(indentedHeight > plainHeight);
        Assert.Equal(35d, indentedBlocks[0].XMillimeters, 3);
        Assert.Equal(55d, indentedBlocks[0].WidthMillimeters, 3);
    }

    [Fact]
    public async Task ResolvedKeepWithNext_IsHonored()
    {
        var keepId = Guid.NewGuid();
        var followingId = Guid.NewGuid();
        var filler = string.Join("\n", Enumerable.Repeat("x", 7));
        var result = await LayoutAsync(Document(
        [
            Text(Guid.NewGuid(), filler, TextFormat()),
            Text(keepId, "Biçimden gelen keep-next", TextFormat(fontSizePoints: 12d, bold: true, keepWithNext: true)),
            Text(followingId, "İzleyen paragraf", TextFormat())
        ], Page(width: 100d, height: 60d, top: 10d, bottom: 10d, left: 10d, right: 10d)));

        var keepPage = result.Pages.Single(page => page.Blocks.Any(block => block.ElementId == keepId));
        var followingPage = result.Pages.First(page => page.Blocks.Any(block => block.ElementId == followingId));

        Assert.Equal(keepPage.PageNumber, followingPage.PageNumber);
        Assert.True(keepPage.PageNumber > 1);
    }

    [Fact]
    public async Task ReferenceColumnWeights_AffectRowMeasurement()
    {
        var longProductName = string.Join(' ', Enumerable.Repeat("Çok uzun ürün açıklaması", 20));
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            new[] { "1", longProductName, "1234", "321", "A222", "2" }
        ];
        var equalId = Guid.NewGuid();
        var weightedId = Guid.NewGuid();
        var equal = await LayoutAsync(Document(
            [Table(equalId, rows, TableFormat([1d, 1d, 1d, 1d, 1d, 1d]))],
            Page(width: 130d, height: 297d, left: 10d, right: 10d)));
        var weighted = await LayoutAsync(Document(
            [Table(weightedId, rows, TableFormat([1d, 8d, 1d, 1d, 1d, 1d]))],
            Page(width: 130d, height: 297d, left: 10d, right: 10d)));

        Assert.True(Block(weighted, weightedId).HeightMillimeters < Block(equal, equalId).HeightMillimeters);
    }

    [Fact]
    public async Task PreferredRowHeight_IsMinimum()
    {
        var tableId = Guid.NewGuid();
        var format = TableFormat([1d], preferredRowHeightMillimeters: 10.195d);
        var result = await LayoutAsync(Document(
        [
            Table(tableId, [new[] { "x" }], format, headers: [])
        ]));

        var block = Block(result, tableId);
        var payload = Assert.IsType<TablePageBlockPayload>(block.Payload);

        Assert.True(block.HeightMillimeters >= 10.195d);
        Assert.Same(format, payload.Format);
    }

    [Fact]
    public async Task CellMargins_AffectWrappingHeight()
    {
        var text = string.Join(' ', Enumerable.Repeat("dar hücre metni", 18));
        IReadOnlyList<IReadOnlyList<string>> rows = [new[] { text }];
        var plainId = Guid.NewGuid();
        var marginedId = Guid.NewGuid();
        var page = Page(width: 60d, height: 297d, left: 10d, right: 10d);
        var plain = await LayoutAsync(Document(
            [Table(plainId, rows, TableFormat([1d]), headers: [])], page));
        var margined = await LayoutAsync(Document(
            [Table(marginedId, rows, TableFormat([1d], cellMarginHorizontalMillimeters: 8d, cellMarginVerticalMillimeters: 1d), headers: [])], page));

        Assert.True(Block(margined, marginedId).HeightMillimeters > Block(plain, plainId).HeightMillimeters);
    }

    [Fact]
    public async Task NoWrapColumn_DoesNotWrapInMeasurement()
    {
        var text = string.Join(' ', Enumerable.Repeat("uzun seri değeri", 25));
        IReadOnlyList<IReadOnlyList<string>> rows = [new[] { text }];
        var wrapId = Guid.NewGuid();
        var noWrapId = Guid.NewGuid();
        var page = Page(width: 55d, height: 297d, left: 10d, right: 10d);
        var wrapped = await LayoutAsync(Document(
            [Table(wrapId, rows, TableFormat([1d]), headers: [])], page));
        var noWrap = await LayoutAsync(Document(
            [Table(noWrapId, rows, TableFormat([1d], noWrapColumns: [0]), headers: [])], page));

        Assert.True(Block(noWrap, noWrapId).HeightMillimeters < Block(wrapped, wrapId).HeightMillimeters);
    }

    [Fact]
    public async Task FormatWarnings_AppearInLayoutWarnings()
    {
        const string warning = "Referans biçim uyarısı";
        var document = Document([Text(Guid.NewGuid(), "x", TextFormat())]);
        document = new ReportContentDocument
        {
            HeaderNodes = document.HeaderNodes,
            BodyNodes = document.BodyNodes,
            FooterNodes = document.FooterNodes,
            TableOfContents = document.TableOfContents,
            PageLayout = document.PageLayout,
            FormatWarnings = [warning, warning, " "]
        };

        var result = await LayoutAsync(document);

        Assert.Equal(1, result.Warnings.Count(candidate => candidate == warning));
    }

    [Fact]
    public async Task ResolvedCaptionFormat_AffectsCaptionMeasurementAndPayload()
    {
        var caption = string.Join(' ', Enumerable.Repeat("uzun referans tablo başlığı", 12));
        var rows = Enumerable.Range(1, 8)
            .Select(index => (IReadOnlyList<string>)new[] { index.ToString(), $"Satır {index}" })
            .ToList();
        var format = TableFormat([1d, 2d]);
        var legacyId = Guid.NewGuid();
        var resolvedId = Guid.NewGuid();
        var captionFormat = TextFormat(
            fontSizePoints: 16d,
            bold: true,
            spaceBeforePoints: 18d,
            spaceAfterPoints: 12d,
            lineSpacingMultiple: 2d,
            leftIndentMillimeters: 8d,
            firstLineIndentMillimeters: 6d,
            keepWithNext: true);
        var page = Page(width: 90d, height: 70d, top: 8d, bottom: 8d, left: 8d, right: 8d);

        var legacy = await LayoutAsync(Document(
            [Table(legacyId, rows, format, headers: ["No", "Açıklama"], caption: caption)],
            page));
        var resolved = await LayoutAsync(Document(
            [Table(resolvedId, rows, format, headers: ["No", "Açıklama"], caption: caption, captionFormat: captionFormat)],
            page));

        var legacyHeight = Blocks(legacy, legacyId).Sum(block => block.HeightMillimeters);
        var resolvedHeight = Blocks(resolved, resolvedId).Sum(block => block.HeightMillimeters);
        Assert.True(resolvedHeight > legacyHeight);
        Assert.True(resolved.Pages.Count >= legacy.Pages.Count);
        var payloads = Blocks(resolved, resolvedId)
            .Select(block => Assert.IsType<TablePageBlockPayload>(block.Payload))
            .ToList();
        Assert.Same(captionFormat, payloads[0].CaptionFormat);
        Assert.NotNull(payloads[0].Caption);
        Assert.All(payloads, payload => Assert.Same(captionFormat, payload.CaptionFormat));
        Assert.All(payloads.Skip(1), payload => Assert.Null(payload.Caption));
    }

    [Fact]
    public async Task Sprint15GroupedPagination_RemainsWithResolvedFormat()
    {
        var tableId = Guid.NewGuid();
        IReadOnlyList<IReadOnlyList<string>> rows = Enumerable.Range(0, 5)
            .Select(index => (IReadOnlyList<string>)new[]
            {
                index == 0 ? "1" : string.Empty,
                index == 0 ? "Elma" : string.Empty,
                index == 0 ? "1234" : string.Empty,
                index == 0 ? "321" : string.Empty,
                $"A22{index}",
                index == 0 ? "5" : string.Empty
            })
            .ToList();
        var spans = new[] { 0, 1, 2, 3, 5 }
            .Select(column => new TableCellSpan { RowIndex = 0, ColumnIndex = column, RowSpan = 5 })
            .ToList();
        var format = TableFormat(
            [469d, 2550d, 1579d, 1579d, 1802d, 1021d],
            widthPercent: 99.32d,
            preferredRowHeightMillimeters: 10.195d,
            cellMarginHorizontalMillimeters: 1.235d,
            noWrapColumns: [0, 1, 2, 3, 4, 5]);
        var table = Table(tableId, rows, format, cellSpans: spans, rowGroups:
        [
            new TableRowGroup { StartRowIndex = 0, RowCount = 5, KeepTogetherWhenPossible = true }
        ]);
        var result = await LayoutAsync(Document(
            [table],
            Page(width: 120d, height: 50d, top: 5d, bottom: 5d, left: 10d, right: 10d)));
        var fragments = Blocks(result, tableId);
        var payloads = fragments.Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload)).ToList();

        Assert.True(payloads.Count >= 2);
        Assert.All(fragments, fragment => Assert.Equal(tableId, fragment.ElementId));
        Assert.All(payloads, payload => Assert.Same(format, payload.Format));
        Assert.Equal(rows.Select(row => row[4]), payloads.SelectMany(payload => payload.Rows).Select(row => row[4]));
        Assert.All(payloads.SelectMany(payload => payload.CellSpans), span =>
        {
            Assert.True(span.RowIndex >= 0);
            var owner = payloads.Single(payload => payload.CellSpans.Contains(span));
            Assert.True(span.RowIndex + span.RowSpan <= owner.Rows.Count);
        });
        Assert.All(payloads.Skip(1), payload => Assert.True(payload.IsHeaderRepeated));
    }

    private static Task<DocumentLayoutResult> LayoutAsync(ReportContentDocument document) =>
        new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

    private static PositionedPageBlock Block(DocumentLayoutResult result, Guid elementId) =>
        Blocks(result, elementId).First();

    private static IReadOnlyList<PositionedPageBlock> Blocks(DocumentLayoutResult result, Guid elementId) =>
        result.Pages
            .SelectMany(page => page.Blocks)
            .Where(block => block.ElementId == elementId)
            .OrderBy(block => block.FragmentIndex)
            .ToList();

    private static ReportContentDocument Document(
        IReadOnlyList<ReportContentNode> bodyNodes,
        PageLayout? pageLayout = null) =>
        new()
        {
            HeaderNodes = [],
            BodyNodes = bodyNodes,
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = pageLayout ?? Page()
        };

    private static TextContentNode Text(Guid id, string text, ResolvedTextFormat format) =>
        new()
        {
            ElementId = id,
            Kind = ReportContentKind.Paragraph,
            Text = text,
            Bold = format.Bold,
            FontSize = format.FontSizePoints,
            Format = format
        };

    private static TableContentNode Table(
        Guid id,
        IReadOnlyList<IReadOnlyList<string>> rows,
        ResolvedTableFormat format,
        IReadOnlyList<string>? headers = null,
        IReadOnlyList<TableCellSpan>? cellSpans = null,
        IReadOnlyList<TableRowGroup>? rowGroups = null,
        string? caption = null,
        ResolvedTextFormat? captionFormat = null) =>
        new()
        {
            ElementId = id,
            Kind = ReportContentKind.Table,
            Name = "Sprint 16 Table",
            Caption = caption,
            CaptionFormat = captionFormat,
            ColumnHeaders = headers ?? ["No", "Product Name", "Product No", "NSN", "Serial No", "Quantity"],
            Rows = rows,
            CellSpans = cellSpans ?? [],
            RowGroups = rowGroups ?? [],
            CompositionWarnings = [],
            Format = format,
            DataSourceName = null,
            SourceCount = 0,
            SourceError = null,
            FilterWasIgnored = false
        };

    private static ResolvedTextFormat TextFormat(
        double fontSizePoints = 10d,
        bool bold = false,
        double spaceBeforePoints = 0d,
        double spaceAfterPoints = 0d,
        double lineSpacingMultiple = 1d,
        double leftIndentMillimeters = 0d,
        double firstLineIndentMillimeters = 0d,
        bool keepWithNext = false) =>
        new()
        {
            FontFamilyName = "Arial",
            FontSizePoints = fontSizePoints,
            Bold = bold,
            Italic = false,
            Underline = false,
            ForegroundColor = "#FF000000",
            Alignment = ParagraphAlignment.Left,
            SpaceBeforePoints = spaceBeforePoints,
            SpaceAfterPoints = spaceAfterPoints,
            LineSpacingMultiple = lineSpacingMultiple,
            LeftIndentMillimeters = leftIndentMillimeters,
            FirstLineIndentMillimeters = firstLineIndentMillimeters,
            KeepWithNext = keepWithNext
        };

    private static ResolvedTableFormat TableFormat(
        IReadOnlyList<double> weights,
        double widthPercent = 100d,
        double preferredRowHeightMillimeters = 0d,
        double cellMarginHorizontalMillimeters = 0d,
        double cellMarginVerticalMillimeters = 0d,
        IReadOnlyCollection<int>? noWrapColumns = null) =>
        new()
        {
            WidthPercent = widthPercent,
            FixedLayout = true,
            BorderSizePoints = 0.5d,
            CellMarginTopMillimeters = cellMarginVerticalMillimeters,
            CellMarginBottomMillimeters = cellMarginVerticalMillimeters,
            CellMarginLeftMillimeters = cellMarginHorizontalMillimeters,
            CellMarginRightMillimeters = cellMarginHorizontalMillimeters,
            PreferredRowHeightMillimeters = preferredRowHeightMillimeters,
            RepeatHeader = true,
            Columns = weights.Select((weight, index) => new ResolvedTableColumnFormat
            {
                WidthWeight = weight,
                HeaderAlignment = index == 1 ? ParagraphAlignment.Left : ParagraphAlignment.Center,
                BodyAlignment = index == 1 ? ParagraphAlignment.Left : ParagraphAlignment.Center,
                HeaderFontFamilyName = "Arial",
                HeaderFontSizePoints = 10d,
                HeaderBold = true,
                BodyFontFamilyName = "Arial",
                BodyFontSizePoints = 10d,
                BodyBold = false,
                VerticalAlignment = VerticalContentAlignment.Center,
                NoWrap = noWrapColumns?.Contains(index) == true
            }).ToList()
        };

    private static PageLayout Page(
        double width = 210d,
        double height = 297d,
        double top = 25d,
        double bottom = 25d,
        double left = 25d,
        double right = 25d) =>
        new()
        {
            WidthMillimeters = width,
            HeightMillimeters = height,
            MarginTopMillimeters = top,
            MarginBottomMillimeters = bottom,
            MarginLeftMillimeters = left,
            MarginRightMillimeters = right,
            HeaderDistanceMillimeters = 12.49d,
            FooterDistanceMillimeters = 12.49d,
            ShowPageNumbers = false
        };
}
