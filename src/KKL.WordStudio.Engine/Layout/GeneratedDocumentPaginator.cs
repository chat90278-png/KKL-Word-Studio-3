namespace KKL.WordStudio.Engine.Layout;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;

internal sealed class GeneratedDocumentPaginator
{
    private readonly DeterministicTextMeasurement _measurement;
    private readonly DeterministicTablePaginator _tablePaginator;

    public GeneratedDocumentPaginator(
        DeterministicTextMeasurement measurement,
        DeterministicTablePaginator tablePaginator)
    {
        _measurement = measurement;
        _tablePaginator = tablePaginator;
    }

    public IReadOnlyList<DocumentPageLayout> Layout(
        ReportContentDocument document,
        int firstPageNumber,
        IReadOnlyList<LaidOutTocEntry> tocEntries,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var flow = new LayoutPageFlow(
            firstPageNumber,
            DocumentPageOrigin.GeneratedReport,
            document.PageLayout,
            page => DecoratePage(page, document, warnings, cancellationToken));
        var captionSequenceCounters = new Dictionary<string, int>(StringComparer.Ordinal);

        if (tocEntries.Count > 0)
            LayoutToc(flow, tocEntries, cancellationToken);

        for (var nodeIndex = 0; nodeIndex < document.BodyNodes.Count; nodeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var node = document.BodyNodes[nodeIndex];

            if (node is TextContentNode text
                && ShouldKeepWithNext(text)
                && nodeIndex + 1 < document.BodyNodes.Count)
            {
                KeepTextWithFollowingBlockWhenPossible(
                    flow,
                    text,
                    document.BodyNodes[nodeIndex + 1]);
            }

            LayoutNode(flow, node, captionSequenceCounters, warnings, cancellationToken);
        }

        return flow.Complete();
    }

    private void DecoratePage(
        MutableLayoutPage page,
        ReportContentDocument document,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var layout = document.PageLayout;
        var contentWidth = Math.Max(
            1d,
            layout.WidthMillimeters
            - Math.Max(0d, layout.MarginLeftMillimeters)
            - Math.Max(0d, layout.MarginRightMillimeters));
        var x = Math.Max(0d, layout.MarginLeftMillimeters);

        LayoutRepeatingRegion(
            page,
            document.HeaderNodes,
            DocumentPageRegion.Header,
            x,
            0d,
            Math.Max(0d, layout.MarginTopMillimeters),
            contentWidth,
            warnings,
            cancellationToken);

        var footerTop = Math.Max(0d, layout.HeightMillimeters - Math.Max(0d, layout.MarginBottomMillimeters));
        var footerBottom = Math.Max(footerTop, layout.HeightMillimeters);
        var pageNumberHeight = layout.ShowPageNumbers
            ? Math.Min(5d, Math.Max(1d, footerBottom - footerTop))
            : 0d;
        var footerContentBottom = layout.ShowPageNumbers
            ? Math.Max(footerTop, footerBottom - pageNumberHeight - 1d)
            : footerBottom;

        LayoutRepeatingRegion(
            page,
            document.FooterNodes,
            DocumentPageRegion.Footer,
            x,
            footerTop,
            footerContentBottom,
            contentWidth,
            warnings,
            cancellationToken);

        if (layout.ShowPageNumbers)
        {
            page.Blocks.Add(new PositionedPageBlock
            {
                ElementId = null,
                Region = DocumentPageRegion.Footer,
                Kind = PageBlockKind.PageNumber,
                XMillimeters = x,
                YMillimeters = Math.Max(0d, footerBottom - pageNumberHeight),
                WidthMillimeters = contentWidth,
                HeightMillimeters = pageNumberHeight,
                FragmentIndex = 0,
                IsContinuation = false,
                IsEditableReportElement = false,
                Payload = new PageNumberPageBlockPayload { PageNumber = page.PageNumber }
            });
        }
    }

    private void LayoutRepeatingRegion(
        MutableLayoutPage page,
        IReadOnlyList<ReportContentNode> nodes,
        DocumentPageRegion region,
        double xMillimeters,
        double regionTopMillimeters,
        double regionBottomMillimeters,
        double contentWidthMillimeters,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var y = regionTopMillimeters;
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = Math.Max(0d, regionBottomMillimeters - y);
            if (remaining <= 0d)
            {
                warnings.Add($"{region} bölgesi dolu olduğu için '{node.ElementId}' öğesi yerleştirilemedi.");
                break;
            }

            var mapped = MapGeneratedNodeAsAtomic(node, contentWidthMillimeters);
            var leftIndent = node is TextContentNode textNode
                ? ResolveLeftIndentMillimeters(textNode, contentWidthMillimeters)
                : 0d;
            var blockWidth = node is TableContentNode tableNode
                ? ResolveTableBlockWidthMillimeters(contentWidthMillimeters, tableNode.Format.WidthPercent)
                : Math.Max(1d, contentWidthMillimeters - leftIndent);
            var height = Math.Min(mapped.HeightMillimeters, remaining);
            page.Blocks.Add(new PositionedPageBlock
            {
                ElementId = node.ElementId,
                Region = region,
                Kind = mapped.Kind,
                XMillimeters = xMillimeters + leftIndent,
                YMillimeters = y,
                WidthMillimeters = blockWidth,
                HeightMillimeters = height,
                FragmentIndex = 0,
                IsContinuation = false,
                IsEditableReportElement = true,
                Payload = mapped.Payload
            });
            y += height + 1d;
        }
    }

    private void LayoutNode(
        LayoutPageFlow flow,
        ReportContentNode node,
        IDictionary<string, int> captionSequenceCounters,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        switch (node)
        {
            case TextContentNode text:
                LayoutText(flow, text, warnings, cancellationToken);
                break;
            case TableContentNode table:
                LayoutTable(flow, table, captionSequenceCounters, warnings, cancellationToken);
                break;
            case ImageContentNode image:
                LayoutImage(flow, image, cancellationToken);
                break;
            default:
                LayoutUnsupported(flow, node, warnings);
                break;
        }
    }

    private void LayoutText(
        LayoutPageFlow flow,
        TextContentNode text,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var format = ResolveEffectiveTextFormat(text);
        var measurement = _measurement.MeasureResolvedText(
            text.Text,
            format,
            flow.ContentWidthMillimeters);
        var spaceBeforeMillimeters = DeterministicTextMeasurement.PointsToMillimeters(format.SpaceBeforePoints);
        var spaceAfterMillimeters = DeterministicTextMeasurement.PointsToMillimeters(format.SpaceAfterPoints);
        var leftIndentMillimeters = ResolveLeftIndentMillimeters(text, flow.ContentWidthMillimeters);
        var blockWidthMillimeters = Math.Max(1d, flow.ContentWidthMillimeters - leftIndentMillimeters);
        var blockXMillimeters = Math.Max(0d, flow.CurrentPage.PageLayout.MarginLeftMillimeters)
            + leftIndentMillimeters;

        var lineIndex = 0;
        var fragmentIndex = 0;
        while (lineIndex < measurement.Lines.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (flow.RemainingBodyHeightMillimeters <= 0d)
                flow.NewPage();

            if (fragmentIndex == 0 && spaceBeforeMillimeters > 0d)
            {
                var firstLineHeight = measurement.Lines[lineIndex].HeightMillimeters;
                if (!flow.IsAtBodyTop
                    && spaceBeforeMillimeters + firstLineHeight > flow.RemainingBodyHeightMillimeters)
                {
                    flow.NewPage();
                    continue;
                }

                var usableSpacing = Math.Min(
                    spaceBeforeMillimeters,
                    Math.Max(0d, flow.RemainingBodyHeightMillimeters - Math.Min(firstLineHeight, flow.BodyHeightMillimeters)));
                flow.AdvanceBody(usableSpacing);
            }

            if (!flow.IsAtBodyTop
                && measurement.Lines[lineIndex].HeightMillimeters > flow.RemainingBodyHeightMillimeters)
            {
                flow.NewPage();
                continue;
            }

            var startLineIndex = lineIndex;
            var height = 0d;
            while (lineIndex < measurement.Lines.Count)
            {
                var lineHeight = Math.Min(
                    measurement.Lines[lineIndex].HeightMillimeters,
                    Math.Max(1d, flow.BodyHeightMillimeters));
                if (height > 0d && height + lineHeight > flow.RemainingBodyHeightMillimeters)
                    break;
                if (height == 0d && lineHeight > flow.RemainingBodyHeightMillimeters)
                    break;

                height += lineHeight;
                lineIndex++;
            }

            if (lineIndex == startLineIndex)
            {
                if (!flow.IsAtBodyTop)
                {
                    flow.NewPage();
                    continue;
                }

                height = Math.Max(1d, flow.BodyHeightMillimeters);
                lineIndex++;
                AddWarningOnce(
                    warnings,
                    $"'{text.ElementId}' metin satırı sayfa gövde yüksekliğini aştığı için görünür gövde yüksekliğine sınırlandı.");
            }

            var fragmentLines = measurement.Lines
                .Skip(startLineIndex)
                .Take(lineIndex - startLineIndex)
                .ToList();
            var isFinalFragment = lineIndex >= measurement.Lines.Count;
            flow.AddBodyBlock(new PositionedPageBlock
            {
                ElementId = text.ElementId,
                Region = DocumentPageRegion.Body,
                Kind = PageBlockKind.Text,
                XMillimeters = blockXMillimeters,
                YMillimeters = flow.BodyYMillimeters,
                WidthMillimeters = blockWidthMillimeters,
                HeightMillimeters = height,
                FragmentIndex = fragmentIndex,
                IsContinuation = fragmentIndex > 0,
                IsEditableReportElement = true,
                Payload = new TextPageBlockPayload
                {
                    Runs = TextRunLayoutFactory.Build(fragmentLines),
                    SemanticKind = text.Kind,
                    Alignment = format.Alignment,
                    Format = format
                }
            }, addGapAfter: isFinalFragment);

            if (isFinalFragment)
                flow.AdvanceBody(spaceAfterMillimeters);

            fragmentIndex++;
            if (!isFinalFragment)
                flow.NewPage();
        }
    }

    private void LayoutTable(
        LayoutPageFlow flow,
        TableContentNode table,
        IDictionary<string, int> captionSequenceCounters,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var warning in table.CompositionWarnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))
            AddWarningOnce(warnings, warning.Trim());

        if (!string.IsNullOrWhiteSpace(table.SourceError))
            AddWarningOnce(warnings, $"'{table.Name}' tablosu kaynak hatası içeriyor: {table.SourceError}");

        var captionSequenceNumber = ResolveCaptionSequenceNumber(table, captionSequenceCounters);
        _tablePaginator.Layout(
            flow,
            table.ElementId,
            table.Name,
            table.Caption,
            table.CaptionFormat,
            table.CaptionSequence,
            captionSequenceNumber,
            table.ColumnHeaders,
            table.Rows,
            table.CellSpans,
            table.RowGroups,
            table.SourceError,
            isEditable: true,
            repeatHeader: table.Format.RepeatHeader,
            format: table.Format,
            warnings,
            cancellationToken);
    }

    private static int? ResolveCaptionSequenceNumber(
        TableContentNode table,
        IDictionary<string, int> captionSequenceCounters)
    {
        if (string.IsNullOrWhiteSpace(table.Caption)
            || table.CaptionSequence is null
            || string.IsNullOrWhiteSpace(table.CaptionSequence.SequenceIdentifier))
        {
            return null;
        }

        var identifier = table.CaptionSequence.SequenceIdentifier;
        captionSequenceCounters.TryGetValue(identifier, out var current);
        var next = current + 1;
        captionSequenceCounters[identifier] = next;
        return next;
    }

    private static void LayoutImage(
        LayoutPageFlow flow,
        ImageContentNode image,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        const double defaultHeight = 40d;
        if (!flow.IsAtBodyTop && defaultHeight > flow.RemainingBodyHeightMillimeters)
            flow.NewPage();

        var height = Math.Min(defaultHeight, Math.Max(1d, flow.BodyHeightMillimeters));
        flow.AddBodyBlock(new PositionedPageBlock
        {
            ElementId = image.ElementId,
            Region = DocumentPageRegion.Body,
            Kind = PageBlockKind.Image,
            XMillimeters = Math.Max(0d, flow.CurrentPage.PageLayout.MarginLeftMillimeters),
            YMillimeters = flow.BodyYMillimeters,
            WidthMillimeters = flow.ContentWidthMillimeters,
            HeightMillimeters = height,
            FragmentIndex = 0,
            IsContinuation = false,
            IsEditableReportElement = true,
            Payload = new ImagePageBlockPayload
            {
                Name = image.Name,
                ImageBytes = null,
                ContentType = null,
                IntrinsicWidthMillimeters = null,
                IntrinsicHeightMillimeters = null
            }
        });
    }

    private static void LayoutUnsupported(
        LayoutPageFlow flow,
        ReportContentNode node,
        List<string> warnings)
    {
        const double desiredHeight = 8d;
        if (!flow.IsAtBodyTop && desiredHeight > flow.RemainingBodyHeightMillimeters)
            flow.NewPage();

        var description = $"Unsupported generated report content node: {node.GetType().Name}";
        warnings.Add(description);
        flow.AddBodyBlock(new PositionedPageBlock
        {
            ElementId = node.ElementId,
            Region = DocumentPageRegion.Body,
            Kind = PageBlockKind.Unsupported,
            XMillimeters = Math.Max(0d, flow.CurrentPage.PageLayout.MarginLeftMillimeters),
            YMillimeters = flow.BodyYMillimeters,
            WidthMillimeters = flow.ContentWidthMillimeters,
            HeightMillimeters = Math.Min(desiredHeight, Math.Max(1d, flow.BodyHeightMillimeters)),
            FragmentIndex = 0,
            IsContinuation = false,
            IsEditableReportElement = true,
            Payload = new UnsupportedPageBlockPayload { Description = description }
        });
    }

    private void KeepTextWithFollowingBlockWhenPossible(
        LayoutPageFlow flow,
        TextContentNode text,
        ReportContentNode followingNode)
    {
        if (flow.IsAtBodyTop)
            return;

        var format = ResolveEffectiveTextFormat(text);
        var textHeight = DeterministicTextMeasurement.PointsToMillimeters(format.SpaceBeforePoints)
            + _measurement.MeasureResolvedText(
                text.Text,
                format,
                flow.ContentWidthMillimeters).TotalHeightMillimeters
            + DeterministicTextMeasurement.PointsToMillimeters(format.SpaceAfterPoints);
        var followingMinimumHeight = EstimateMinimumFragmentHeight(
            followingNode,
            flow.ContentWidthMillimeters,
            flow.BodyHeightMillimeters);
        var combinedHeight = textHeight + followingMinimumHeight;

        if (combinedHeight > flow.RemainingBodyHeightMillimeters
            && combinedHeight <= flow.BodyHeightMillimeters)
        {
            flow.NewPage();
        }
    }

    private double EstimateMinimumFragmentHeight(
        ReportContentNode node,
        double contentWidthMillimeters,
        double freshBodyHeightMillimeters) =>
        node switch
        {
            TextContentNode text => EstimateMinimumTextFragmentHeight(text, contentWidthMillimeters),
            TableContentNode table => _tablePaginator.EstimateMinimumFragmentHeight(
                table.Caption,
                table.CaptionFormat,
                table.ColumnHeaders,
                table.Rows,
                table.RowGroups,
                table.Format,
                contentWidthMillimeters,
                freshBodyHeightMillimeters),
            ImageContentNode => Math.Min(40d, contentWidthMillimeters),
            _ => 8d
        };

    private double EstimateMinimumTextFragmentHeight(
        TextContentNode text,
        double contentWidthMillimeters)
    {
        var format = ResolveEffectiveTextFormat(text);
        return DeterministicTextMeasurement.PointsToMillimeters(format.SpaceBeforePoints)
            + _measurement.MeasureResolvedText(text.Text, format, contentWidthMillimeters).FirstLineHeightMillimeters;
    }

    private void LayoutToc(
        LayoutPageFlow flow,
        IReadOnlyList<LaidOutTocEntry> entries,
        CancellationToken cancellationToken)
    {
        var entryIndex = 0;
        var fragmentIndex = 0;
        while (entryIndex < entries.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (flow.RemainingBodyHeightMillimeters <= 0d)
                flow.NewPage();

            var startEntryIndex = entryIndex;
            var height = 0d;
            while (entryIndex < entries.Count)
            {
                var entry = entries[entryIndex];
                var indent = Math.Max(0, entry.Level - 1) * 6d;
                var entryHeight = _measurement.EstimatePlainTextHeight(
                    $"{entry.Text} {entry.PageNumber}",
                    10d,
                    Math.Max(1d, flow.ContentWidthMillimeters - indent));
                if (height > 0d && height + entryHeight > flow.RemainingBodyHeightMillimeters)
                    break;
                if (height == 0d && entryHeight > flow.RemainingBodyHeightMillimeters)
                    break;

                height += entryHeight;
                entryIndex++;
            }

            if (entryIndex == startEntryIndex)
            {
                if (!flow.IsAtBodyTop)
                {
                    flow.NewPage();
                    continue;
                }

                height = Math.Max(1d, flow.BodyHeightMillimeters);
                entryIndex++;
            }

            flow.AddBodyBlock(new PositionedPageBlock
            {
                ElementId = null,
                Region = DocumentPageRegion.Body,
                Kind = PageBlockKind.TableOfContents,
                XMillimeters = Math.Max(0d, flow.CurrentPage.PageLayout.MarginLeftMillimeters),
                YMillimeters = flow.BodyYMillimeters,
                WidthMillimeters = flow.ContentWidthMillimeters,
                HeightMillimeters = height,
                FragmentIndex = fragmentIndex,
                IsContinuation = fragmentIndex > 0,
                IsEditableReportElement = false,
                Payload = new TocPageBlockPayload
                {
                    Entries = entries.Skip(startEntryIndex).Take(entryIndex - startEntryIndex).ToList()
                }
            }, addGapAfter: entryIndex >= entries.Count);

            fragmentIndex++;
            if (entryIndex < entries.Count)
                flow.NewPage();
        }
    }

    private AtomicGeneratedBlock MapGeneratedNodeAsAtomic(
        ReportContentNode node,
        double widthMillimeters) =>
        node switch
        {
            TextContentNode text => MapAtomicText(text, widthMillimeters),
            TableContentNode table => new AtomicGeneratedBlock(
                PageBlockKind.Table,
                _tablePaginator.EstimateAtomicHeight(
                    table.Caption,
                    table.CaptionFormat,
                    table.ColumnHeaders,
                    table.Rows,
                    table.Format,
                    widthMillimeters),
                new TablePageBlockPayload
                {
                    Name = table.Name,
                    Caption = table.Caption,
                    CaptionFormat = table.CaptionFormat,
                    CaptionSequence = table.CaptionSequence,
                    ColumnHeaders = table.ColumnHeaders,
                    Rows = table.Rows,
                    CellSpans = table.CellSpans,
                    Format = table.Format,
                    StartRowIndex = 0,
                    HasHeader = table.ColumnHeaders.Count > 0,
                    IsHeaderRepeated = false,
                    SourceError = table.SourceError
                }),
            ImageContentNode image => new AtomicGeneratedBlock(
                PageBlockKind.Image,
                25d,
                new ImagePageBlockPayload
                {
                    Name = image.Name,
                    ImageBytes = null,
                    ContentType = null,
                    IntrinsicWidthMillimeters = null,
                    IntrinsicHeightMillimeters = null
                }),
            _ => new AtomicGeneratedBlock(
                PageBlockKind.Unsupported,
                8d,
                new UnsupportedPageBlockPayload
                {
                    Description = $"Unsupported generated report content node: {node.GetType().Name}"
                })
        };

    private AtomicGeneratedBlock MapAtomicText(TextContentNode text, double widthMillimeters)
    {
        var format = ResolveEffectiveTextFormat(text);
        var measurement = _measurement.MeasureResolvedText(text.Text, format, widthMillimeters);
        var paragraphHeight = DeterministicTextMeasurement.PointsToMillimeters(format.SpaceBeforePoints)
            + measurement.TotalHeightMillimeters
            + DeterministicTextMeasurement.PointsToMillimeters(format.SpaceAfterPoints);
        return new AtomicGeneratedBlock(
            PageBlockKind.Text,
            paragraphHeight,
            new TextPageBlockPayload
            {
                Runs = TextRunLayoutFactory.Build(measurement.Lines),
                SemanticKind = text.Kind,
                Alignment = format.Alignment,
                Format = format
            });
    }

    private static bool ShouldKeepWithNext(TextContentNode text)
    {
        var format = ResolveEffectiveTextFormat(text);
        return format.KeepWithNext;
    }

    private static double ResolveLeftIndentMillimeters(
        TextContentNode text,
        double contentWidthMillimeters)
    {
        var format = ResolveEffectiveTextFormat(text);
        return Math.Min(
            Math.Max(0d, contentWidthMillimeters - 1d),
            Math.Max(0d, format.LeftIndentMillimeters));
    }

    private static double ResolveTableBlockWidthMillimeters(double contentWidthMillimeters, double widthPercent)
    {
        var normalizedPercent = widthPercent > 0d ? Math.Min(widthPercent, 100d) : 100d;
        return Math.Max(1d, contentWidthMillimeters * normalizedPercent / 100d);
    }

    private static ResolvedTextFormat ResolveEffectiveTextFormat(TextContentNode text)
    {
        var format = text.Format;
        if (!ReferenceEquals(format, DefaultFormatProfiles.BodyText))
            return format;

        return new ResolvedTextFormat
        {
            FontFamilyName = format.FontFamilyName,
            FontSizePoints = text.FontSize > 0d ? text.FontSize : format.FontSizePoints,
            Bold = text.Bold,
            Italic = format.Italic,
            Underline = format.Underline,
            ForegroundColor = format.ForegroundColor,
            Alignment = format.Alignment,
            SpaceBeforePoints = format.SpaceBeforePoints,
            SpaceAfterPoints = format.SpaceAfterPoints,
            LineSpacingMultiple = format.LineSpacingMultiple,
            LeftIndentMillimeters = format.LeftIndentMillimeters,
            FirstLineIndentMillimeters = format.FirstLineIndentMillimeters,
            KeepWithNext = text.Kind is ReportContentKind.Heading or ReportContentKind.AltHeading
        };
    }

    private static void AddWarningOnce(List<string> warnings, string warning)
    {
        if (!warnings.Any(existing => string.Equals(existing, warning, StringComparison.Ordinal)))
            warnings.Add(warning);
    }

    private sealed record AtomicGeneratedBlock(
        PageBlockKind Kind,
        double HeightMillimeters,
        PageBlockPayload Payload);
}
