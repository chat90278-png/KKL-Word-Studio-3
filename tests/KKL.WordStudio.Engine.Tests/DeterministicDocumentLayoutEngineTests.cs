namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.ImportedDocuments;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class DeterministicDocumentLayoutEngineTests
{
    [Fact]
    public async Task LongBodyText_FlowsAcrossMultiplePages()
    {
        var elementId = Guid.NewGuid();
        var document = CreateDocument(
            bodyNodes:
            [
                TextNode(elementId, string.Join(' ', Enumerable.Repeat("uzun paragraf içeriği", 500)))
            ],
            pageLayout: CreatePageLayout(width: 100, height: 100, top: 10, bottom: 10, left: 10, right: 10));

        var result = await LayoutAsync(document);

        Assert.True(result.Pages.Count > 1);
        var fragments = result.Pages.SelectMany(page => page.Blocks)
            .Where(block => block.ElementId == elementId)
            .ToList();
        Assert.True(fragments.Count > 1);
        Assert.Equal(Enumerable.Range(0, fragments.Count), fragments.Select(block => block.FragmentIndex));
        Assert.False(fragments[0].IsContinuation);
        Assert.All(fragments.Skip(1), fragment => Assert.True(fragment.IsContinuation));
    }

    [Fact]
    public async Task GeneratedPages_UseConfiguredPageGeometryAndMargins()
    {
        var layout = CreatePageLayout(width: 160, height: 220, top: 18, bottom: 22, left: 17, right: 19);
        var document = CreateDocument(
            bodyNodes:
            [
                TextNode(Guid.NewGuid(), string.Join(' ', Enumerable.Repeat("geometry", 800)))
            ],
            pageLayout: layout);

        var result = await LayoutAsync(document);

        Assert.All(result.Pages, page => Assert.Same(layout, page.PageLayout));
        foreach (var block in result.Pages.SelectMany(page => page.Blocks).Where(block => block.Region == DocumentPageRegion.Body))
        {
            Assert.True(block.XMillimeters >= layout.MarginLeftMillimeters);
            Assert.True(block.YMillimeters >= layout.MarginTopMillimeters);
            Assert.True(block.XMillimeters + block.WidthMillimeters <= layout.WidthMillimeters - layout.MarginRightMillimeters + 0.001d);
            Assert.True(block.YMillimeters + block.HeightMillimeters <= layout.HeightMillimeters - layout.MarginBottomMillimeters + 0.001d);
        }
    }

    [Fact]
    public async Task HeaderAndFooter_RepeatOnEveryGeneratedPage()
    {
        var headerId = Guid.NewGuid();
        var footerId = Guid.NewGuid();
        var document = CreateDocument(
            headerNodes: [TextNode(headerId, "Başlık")],
            bodyNodes:
            [
                TextNode(Guid.NewGuid(), string.Join(' ', Enumerable.Repeat("body", 1000)))
            ],
            footerNodes: [TextNode(footerId, "Alt bilgi")],
            pageLayout: CreatePageLayout(width: 100, height: 100, top: 12, bottom: 12, left: 10, right: 10));

        var result = await LayoutAsync(document);

        Assert.True(result.Pages.Count > 1);
        Assert.All(result.Pages, page =>
        {
            Assert.Contains(page.Blocks, block => block.ElementId == headerId && block.Region == DocumentPageRegion.Header);
            Assert.Contains(page.Blocks, block => block.ElementId == footerId && block.Region == DocumentPageRegion.Footer);
        });
    }

    [Fact]
    public async Task PageNumbers_AreAbsoluteAndSequential()
    {
        var frontMatter = new ImportedDocumentPreviewDocument
        {
            Warnings = [],
            Sections =
            [
                new ImportedDocumentSection
                {
                    PageLayout = CreatePageLayout(),
                    Blocks =
                    [
                        ImportedParagraph("Ön kapak"),
                        new ImportedExplicitPageBreakBlock(),
                        ImportedParagraph("İkinci ön sayfa")
                    ]
                }
            ]
        };
        var document = CreateDocument(
            bodyNodes:
            [
                TextNode(Guid.NewGuid(), string.Join(' ', Enumerable.Repeat("generated", 900)))
            ],
            pageLayout: CreatePageLayout(width: 100, height: 100, top: 10, bottom: 10, left: 10, right: 10, showPageNumbers: true));

        var result = await LayoutAsync(document, frontMatter);

        Assert.Equal(Enumerable.Range(1, result.Pages.Count), result.Pages.Select(page => page.PageNumber));
        var generatedPages = result.Pages.Where(page => page.Origin == DocumentPageOrigin.GeneratedReport).ToList();
        Assert.All(generatedPages, page =>
        {
            var block = Assert.Single(
                page.Blocks,
                candidate => candidate.Kind == PageBlockKind.PageNumber);
            Assert.Null(block.ElementId);
            Assert.False(block.IsEditableReportElement);
            Assert.Equal(page.PageNumber, Assert.IsType<PageNumberPageBlockPayload>(block.Payload).PageNumber);
        });
        Assert.Equal(3, generatedPages[0].PageNumber);
    }

    [Fact]
    public async Task Heading_IsKeptWithFollowingBlockWhenPossible()
    {
        var fillerId = Guid.NewGuid();
        var headingId = Guid.NewGuid();
        var followingId = Guid.NewGuid();
        var filler = string.Join("\n", Enumerable.Repeat("x", 8));
        var document = CreateDocument(
            bodyNodes:
            [
                TextNode(fillerId, filler),
                TextNode(headingId, "Başlık", ReportContentKind.Heading, bold: true, fontSize: 14),
                TextNode(followingId, "Başlığı izleyen paragraf")
            ],
            pageLayout: CreatePageLayout(width: 100, height: 80, top: 15, bottom: 15, left: 10, right: 10));

        var result = await LayoutAsync(document);

        var headingPage = result.Pages.Single(page => page.Blocks.Any(block => block.ElementId == headingId));
        var followingPage = result.Pages.First(page => page.Blocks.Any(block => block.ElementId == followingId));
        Assert.Equal(headingPage.PageNumber, followingPage.PageNumber);
        Assert.True(headingPage.PageNumber > 1);
    }

    [Fact]
    public async Task Table_SplitsRowsAcrossPages()
    {
        var tableId = Guid.NewGuid();
        var rows = Enumerable.Range(0, 30)
            .Select(index => (IReadOnlyList<string>)new[] { index.ToString(), $"Satır {index}" })
            .ToList();
        var document = CreateDocument(
            bodyNodes: [TableNode(tableId, rows)],
            pageLayout: CreatePageLayout(width: 100, height: 80, top: 10, bottom: 10, left: 10, right: 10));

        var result = await LayoutAsync(document);

        var fragments = TableFragments(result, tableId);
        Assert.True(fragments.Count > 1);
        Assert.Equal(rows.Count, fragments.Sum(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload).Rows.Count));
        Assert.Equal(Enumerable.Range(0, fragments.Count), fragments.Select(fragment => fragment.FragmentIndex));
    }

    [Fact]
    public async Task TableContinuation_RepeatsHeader()
    {
        var tableId = Guid.NewGuid();
        var rows = Enumerable.Range(0, 30)
            .Select(index => (IReadOnlyList<string>)new[] { index.ToString(), $"Satır {index}" })
            .ToList();
        var document = CreateDocument(
            bodyNodes: [TableNode(tableId, rows)],
            pageLayout: CreatePageLayout(width: 100, height: 80, top: 10, bottom: 10, left: 10, right: 10));

        var result = await LayoutAsync(document);
        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.True(payloads.Count > 1);
        Assert.True(payloads[0].HasHeader);
        Assert.False(payloads[0].IsHeaderRepeated);
        Assert.All(payloads.Skip(1), payload =>
        {
            Assert.True(payload.HasHeader);
            Assert.True(payload.IsHeaderRepeated);
            Assert.Equal(new[] { "No", "Açıklama" }, payload.ColumnHeaders);
        });
    }

    [Fact]
    public async Task TableCaption_AppearsOnlyOnFirstFragment()
    {
        var tableId = Guid.NewGuid();
        var rows = Enumerable.Range(0, 30)
            .Select(index => (IReadOnlyList<string>)new[] { index.ToString(), $"Satır {index}" })
            .ToList();
        var document = CreateDocument(
            bodyNodes: [TableNode(tableId, rows, caption: "Tablo 1")],
            pageLayout: CreatePageLayout(width: 100, height: 80, top: 10, bottom: 10, left: 10, right: 10));

        var result = await LayoutAsync(document);
        var payloads = TableFragments(result, tableId)
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload))
            .ToList();

        Assert.Equal("Tablo 1", payloads[0].Caption);
        Assert.All(payloads.Skip(1), payload => Assert.Null(payload.Caption));
    }

    [Fact]
    public async Task TableFragments_PreserveElementIdAndFragmentOrder()
    {
        var tableId = Guid.NewGuid();
        var rows = Enumerable.Range(0, 25)
            .Select(index => (IReadOnlyList<string>)new[] { index.ToString(), new string('a', 40) })
            .ToList();
        var document = CreateDocument(
            bodyNodes: [TableNode(tableId, rows)],
            pageLayout: CreatePageLayout(width: 100, height: 80, top: 10, bottom: 10, left: 10, right: 10));

        var result = await LayoutAsync(document);
        var fragments = TableFragments(result, tableId);

        Assert.All(fragments, fragment =>
        {
            Assert.Equal(tableId, fragment.ElementId);
            Assert.True(fragment.IsEditableReportElement);
        });
        Assert.Equal(Enumerable.Range(0, fragments.Count), fragments.Select(fragment => fragment.FragmentIndex));
        Assert.False(fragments[0].IsContinuation);
        Assert.All(fragments.Skip(1), fragment => Assert.True(fragment.IsContinuation));

        var startRows = fragments
            .Select(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload).StartRowIndex)
            .ToList();
        Assert.Equal(startRows.OrderBy(value => value), startRows);
    }

    [Fact]
    public async Task OversizedTableRow_ProducesWarningWithoutInfiniteLoop()
    {
        var tableId = Guid.NewGuid();
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "1", string.Join(' ', Enumerable.Repeat("çok uzun hücre", 1000)) },
            new[] { "2", "normal" }
        };
        var document = CreateDocument(
            bodyNodes: [TableNode(tableId, rows)],
            pageLayout: CreatePageLayout(width: 80, height: 70, top: 10, bottom: 10, left: 10, right: 10));

        var result = await LayoutAsync(document);

        Assert.Contains(result.Warnings, warning => warning.Contains("sayfa gövde yüksekliğini aşıyor", StringComparison.Ordinal));
        var fragments = TableFragments(result, tableId);
        Assert.InRange(fragments.Count, 1, 5);
        Assert.Equal(2, fragments.Sum(fragment => Assert.IsType<TablePageBlockPayload>(fragment.Payload).Rows.Count));
    }

    [Fact]
    public async Task TocEntries_UseFirstElementPageNumbers()
    {
        var heading1 = Guid.NewGuid();
        var heading2 = Guid.NewGuid();
        var document = CreateDocument(
            bodyNodes:
            [
                TextNode(heading1, "Birinci Başlık", ReportContentKind.Heading, bold: true, fontSize: 14),
                TextNode(Guid.NewGuid(), string.Join(' ', Enumerable.Repeat("content", 600))),
                TextNode(heading2, "İkinci Başlık", ReportContentKind.Heading, bold: true, fontSize: 14)
            ],
            tableOfContents:
            [
                new TocEntry { ElementId = heading1, Text = "Birinci Başlık", Level = 1 },
                new TocEntry { ElementId = heading2, Text = "İkinci Başlık", Level = 1 }
            ],
            pageLayout: CreatePageLayout(width: 100, height: 100, top: 10, bottom: 10, left: 10, right: 10));

        var result = await LayoutAsync(document);
        var firstPages = result.Pages
            .SelectMany(page => page.Blocks.Select(block => new { page.PageNumber, block.ElementId }))
            .Where(item => item.ElementId is not null)
            .GroupBy(item => item.ElementId!.Value)
            .ToDictionary(group => group.Key, group => group.Min(item => item.PageNumber));
        var tocEntries = result.Pages.SelectMany(page => page.Blocks)
            .Where(block => block.Kind == PageBlockKind.TableOfContents)
            .SelectMany(block => Assert.IsType<TocPageBlockPayload>(block.Payload).Entries)
            .ToList();

        Assert.Equal(firstPages[heading1], tocEntries.Single(entry => entry.ElementId == heading1).PageNumber);
        Assert.Equal(firstPages[heading2], tocEntries.Single(entry => entry.ElementId == heading2).PageNumber);
    }

    [Fact]
    public async Task FrontMatterPages_PrecedeGeneratedPages()
    {
        var frontMatter = FrontMatterDocument(
            CreatePageLayout(),
            [ImportedParagraph("Ön içerik")]);
        var document = CreateDocument(bodyNodes: [TextNode(Guid.NewGuid(), "Rapor")]);

        var result = await LayoutAsync(document, frontMatter);

        Assert.Equal(DocumentPageOrigin.FrontMatter, result.Pages[0].Origin);
        Assert.Equal(DocumentPageOrigin.GeneratedReport, result.Pages[^1].Origin);
        Assert.False(result.Pages[0].Blocks.Single().IsEditableReportElement);
    }

    [Fact]
    public async Task FrontMatterExplicitBreak_ForcesNewPage()
    {
        var frontMatter = FrontMatterDocument(
            CreatePageLayout(),
            [
                ImportedParagraph("Bir"),
                new ImportedExplicitPageBreakBlock(),
                ImportedParagraph("İki")
            ]);

        var result = await LayoutAsync(CreateDocument(), frontMatter);
        var frontPages = result.Pages.Where(page => page.Origin == DocumentPageOrigin.FrontMatter).ToList();

        Assert.Equal(2, frontPages.Count);
        Assert.Contains(frontPages[0].Blocks, block => Text(block).Contains("Bir", StringComparison.Ordinal));
        Assert.Contains(frontPages[1].Blocks, block => Text(block).Contains("İki", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FrontMatterSection_UsesOwnPageLayout()
    {
        var firstLayout = CreatePageLayout(width: 120, height: 180, top: 12, bottom: 12, left: 14, right: 14);
        var secondLayout = CreatePageLayout(width: 210, height: 297, top: 20, bottom: 20, left: 20, right: 20);
        var frontMatter = new ImportedDocumentPreviewDocument
        {
            Warnings = [],
            Sections =
            [
                new ImportedDocumentSection { PageLayout = firstLayout, Blocks = [ImportedParagraph("Bir")] },
                new ImportedDocumentSection { PageLayout = secondLayout, Blocks = [ImportedParagraph("İki")] }
            ]
        };

        var result = await LayoutAsync(CreateDocument(), frontMatter);
        var frontPages = result.Pages.Where(page => page.Origin == DocumentPageOrigin.FrontMatter).ToList();

        Assert.Same(firstLayout, frontPages[0].PageLayout);
        Assert.Same(secondLayout, frontPages[1].PageLayout);
    }

    [Fact]
    public async Task ImportedImage_IsFitWithinPageBody()
    {
        var layout = CreatePageLayout(width: 100, height: 100, top: 10, bottom: 10, left: 10, right: 10);
        var frontMatter = FrontMatterDocument(
            layout,
            [
                new ImportedImageBlock
                {
                    Name = "image.png",
                    ImageBytes = [1, 2, 3],
                    ContentType = "image/png",
                    WidthMillimeters = 400,
                    HeightMillimeters = 200
                }
            ]);

        var result = await LayoutAsync(CreateDocument(), frontMatter);
        var image = Assert.Single(result.Pages[0].Blocks);

        Assert.Equal(PageBlockKind.Image, image.Kind);
        Assert.True(image.WidthMillimeters <= 80d);
        Assert.True(image.HeightMillimeters <= 80d);
        Assert.Equal(2d, image.WidthMillimeters / image.HeightMillimeters, 5);
        var payload = Assert.IsType<ImagePageBlockPayload>(image.Payload);
        Assert.Equal(400d, payload.IntrinsicWidthMillimeters);
        Assert.Equal(200d, payload.IntrinsicHeightMillimeters);
    }

    [Fact]
    public async Task UnsupportedImportedBlock_IsVisibleAndWarned()
    {
        var frontMatter = FrontMatterDocument(
            CreatePageLayout(),
            [new ImportedUnsupportedBlock { Description = "SmartArt" }]);

        var result = await LayoutAsync(CreateDocument(), frontMatter);
        var block = Assert.Single(result.Pages[0].Blocks);

        Assert.Equal(PageBlockKind.Unsupported, block.Kind);
        Assert.Equal("SmartArt", Assert.IsType<UnsupportedPageBlockPayload>(block.Payload).Description);
        Assert.Contains(result.Warnings, warning => warning.Contains("SmartArt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LayoutBlocks_DoNotOverlapWithinBodyFlow()
    {
        var tableId = Guid.NewGuid();
        var document = CreateDocument(
            bodyNodes:
            [
                TextNode(Guid.NewGuid(), string.Join(' ', Enumerable.Repeat("paragraph", 50))),
                TableNode(
                    tableId,
                    Enumerable.Range(0, 25)
                        .Select(index => (IReadOnlyList<string>)new[] { index.ToString(), $"row {index}" })
                        .ToList()),
                TextNode(Guid.NewGuid(), string.Join(' ', Enumerable.Repeat("tail", 70)))
            ],
            pageLayout: CreatePageLayout(width: 100, height: 100, top: 10, bottom: 10, left: 10, right: 10));

        var result = await LayoutAsync(document);

        foreach (var page in result.Pages)
        {
            var bodyBlocks = page.Blocks
                .Where(block => block.Region == DocumentPageRegion.Body)
                .OrderBy(block => block.YMillimeters)
                .ToList();
            for (var index = 1; index < bodyBlocks.Count; index++)
            {
                Assert.True(
                    bodyBlocks[index - 1].YMillimeters + bodyBlocks[index - 1].HeightMillimeters
                    <= bodyBlocks[index].YMillimeters + 0.001d);
            }
        }
    }

    [Fact]
    public async Task Cancellation_IsObservedDuringLargeTableLayout()
    {
        var rows = Enumerable.Range(0, 100_000)
            .Select(index => (IReadOnlyList<string>)new[]
            {
                index.ToString(),
                new string('x', 200)
            })
            .ToList();
        var document = CreateDocument(
            bodyNodes: [TableNode(Guid.NewGuid(), rows)],
            pageLayout: CreatePageLayout(width: 100, height: 100, top: 10, bottom: 10, left: 10, right: 10));
        var request = new DocumentLayoutRequest { ReportContent = document, FrontMatter = null };
        var engine = new DeterministicDocumentLayoutEngine();
        using var cancellation = new CancellationTokenSource();

        var layoutTask = Task.Run(() => engine.LayoutAsync(request, cancellation.Token));
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => layoutTask);
    }

    private static Task<DocumentLayoutResult> LayoutAsync(
        ReportContentDocument document,
        ImportedDocumentPreviewDocument? frontMatter = null) =>
        new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = frontMatter
        });

    private static IReadOnlyList<PositionedPageBlock> TableFragments(
        DocumentLayoutResult result,
        Guid tableId) =>
        result.Pages.SelectMany(page => page.Blocks)
            .Where(block => block.ElementId == tableId && block.Kind == PageBlockKind.Table)
            .ToList();

    private static string Text(PositionedPageBlock block) =>
        block.Payload is TextPageBlockPayload payload
            ? string.Concat(payload.Runs.Select(run => run.Text))
            : string.Empty;

    private static ReportContentDocument CreateDocument(
        IReadOnlyList<ReportContentNode>? headerNodes = null,
        IReadOnlyList<ReportContentNode>? bodyNodes = null,
        IReadOnlyList<ReportContentNode>? footerNodes = null,
        IReadOnlyList<TocEntry>? tableOfContents = null,
        PageLayout? pageLayout = null) =>
        new()
        {
            HeaderNodes = headerNodes ?? [],
            BodyNodes = bodyNodes ?? [],
            FooterNodes = footerNodes ?? [],
            TableOfContents = tableOfContents ?? [],
            PageLayout = pageLayout ?? CreatePageLayout()
        };

    private static TextContentNode TextNode(
        Guid elementId,
        string text,
        ReportContentKind kind = ReportContentKind.Paragraph,
        bool bold = false,
        double fontSize = 11d) =>
        new()
        {
            ElementId = elementId,
            Kind = kind,
            Text = text,
            Bold = bold,
            FontSize = fontSize
        };

    private static TableContentNode TableNode(
        Guid elementId,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string? caption = null) =>
        new()
        {
            ElementId = elementId,
            Kind = ReportContentKind.Table,
            Name = "Test Table",
            Caption = caption,
            ColumnHeaders = ["No", "Açıklama"],
            Rows = rows,
            DataSourceName = null,
            SourceCount = 0,
            SourceError = null,
            FilterWasIgnored = false
        };

    private static ImportedParagraphBlock ImportedParagraph(string text) =>
        new()
        {
            Runs =
            [
                new ImportedTextRun
                {
                    Text = text,
                    Bold = false,
                    Italic = false,
                    Underline = false,
                    FontSizePoints = 11d,
                    FontFamilyName = "Calibri"
                }
            ],
            Alignment = ParagraphAlignment.Left,
            KeepWithNext = false
        };

    private static ImportedDocumentPreviewDocument FrontMatterDocument(
        PageLayout pageLayout,
        IReadOnlyList<ImportedDocumentBlock> blocks) =>
        new()
        {
            Warnings = [],
            Sections =
            [
                new ImportedDocumentSection
                {
                    PageLayout = pageLayout,
                    Blocks = blocks
                }
            ]
        };

    private static PageLayout CreatePageLayout(
        double width = 210,
        double height = 297,
        double top = 20,
        double bottom = 20,
        double left = 20,
        double right = 20,
        bool showPageNumbers = false) =>
        new()
        {
            WidthMillimeters = width,
            HeightMillimeters = height,
            MarginTopMillimeters = top,
            MarginBottomMillimeters = bottom,
            MarginLeftMillimeters = left,
            MarginRightMillimeters = right,
            ShowPageNumbers = showPageNumbers
        };
}
