namespace KKL.WordStudio.Engine.Layout;

using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.ImportedDocuments;
using KKL.WordStudio.Application.Layout;

internal sealed class FrontMatterPaginator
{
    private readonly DeterministicTextMeasurement _measurement;
    private readonly DeterministicTablePaginator _tablePaginator;

    public FrontMatterPaginator(
        DeterministicTextMeasurement measurement,
        DeterministicTablePaginator tablePaginator)
    {
        _measurement = measurement;
        _tablePaginator = tablePaginator;
    }

    public IReadOnlyList<DocumentPageLayout> Layout(
        ImportedDocumentPreviewDocument document,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var pages = new List<DocumentPageLayout>();
        var nextPageNumber = 1;

        foreach (var section in document.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sectionPages = LayoutSection(
                section,
                nextPageNumber,
                warnings,
                cancellationToken);
            pages.AddRange(sectionPages);
            nextPageNumber += sectionPages.Count;
        }

        return pages;
    }

    private IReadOnlyList<DocumentPageLayout> LayoutSection(
        ImportedDocumentSection section,
        int firstPageNumber,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var flow = new LayoutPageFlow(
            firstPageNumber,
            DocumentPageOrigin.FrontMatter,
            section.PageLayout);

        for (var blockIndex = 0; blockIndex < section.Blocks.Count; blockIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var block = section.Blocks[blockIndex];

            if (block is ImportedParagraphBlock keepParagraph
                && keepParagraph.KeepWithNext
                && blockIndex + 1 < section.Blocks.Count)
            {
                KeepParagraphWithNextWhenPossible(
                    flow,
                    keepParagraph,
                    section.Blocks[blockIndex + 1]);
            }

            switch (block)
            {
                case ImportedParagraphBlock paragraph:
                    LayoutParagraph(flow, paragraph, warnings, cancellationToken);
                    break;
                case ImportedTableBlock table:
                    LayoutTable(flow, table, warnings, cancellationToken);
                    break;
                case ImportedImageBlock image:
                    LayoutImage(flow, image, cancellationToken);
                    break;
                case ImportedExplicitPageBreakBlock:
                    flow.NewPage();
                    break;
                case ImportedUnsupportedBlock unsupported:
                    LayoutUnsupported(flow, unsupported, warnings);
                    break;
                default:
                    LayoutUnsupported(
                        flow,
                        new ImportedUnsupportedBlock
                        {
                            Description = $"Desteklenmeyen içe aktarılan blok: {block.GetType().Name}"
                        },
                        warnings);
                    break;
            }
        }

        return flow.Complete();
    }

    private void LayoutParagraph(
        LayoutPageFlow flow,
        ImportedParagraphBlock paragraph,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var measurement = _measurement.Measure(
            MapRuns(paragraph.Runs),
            flow.ContentWidthMillimeters);

        var lineIndex = 0;
        var fragmentIndex = 0;
        while (lineIndex < measurement.Lines.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (flow.RemainingBodyHeightMillimeters <= 0d)
                flow.NewPage();

            if (!flow.IsAtBodyTop
                && measurement.Lines[lineIndex].HeightMillimeters > flow.RemainingBodyHeightMillimeters)
            {
                flow.NewPage();
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
                warnings.Add("İçe aktarılan paragrafın bir satırı sayfa gövde yüksekliğini aştığı için görünür gövde yüksekliğine sınırlandı.");
            }

            var fragmentLines = measurement.Lines
                .Skip(startLineIndex)
                .Take(lineIndex - startLineIndex)
                .ToList();
            flow.AddBodyBlock(new PositionedPageBlock
            {
                ElementId = null,
                Region = DocumentPageRegion.Body,
                Kind = PageBlockKind.Text,
                XMillimeters = Math.Max(0d, flow.CurrentPage.PageLayout.MarginLeftMillimeters),
                YMillimeters = flow.BodyYMillimeters,
                WidthMillimeters = flow.ContentWidthMillimeters,
                HeightMillimeters = height,
                FragmentIndex = fragmentIndex,
                IsContinuation = fragmentIndex > 0,
                IsEditableReportElement = false,
                Payload = new TextPageBlockPayload
                {
                    Runs = TextRunLayoutFactory.Build(fragmentLines),
                    SemanticKind = null,
                    Alignment = paragraph.Alignment
                }
            }, addGapAfter: lineIndex >= measurement.Lines.Count);

            fragmentIndex++;
            if (lineIndex < measurement.Lines.Count)
                flow.NewPage();
        }
    }

    private void KeepParagraphWithNextWhenPossible(
        LayoutPageFlow flow,
        ImportedParagraphBlock paragraph,
        ImportedDocumentBlock followingBlock)
    {
        if (flow.IsAtBodyTop)
            return;

        var paragraphHeight = _measurement.Measure(
            MapRuns(paragraph.Runs),
            flow.ContentWidthMillimeters).TotalHeightMillimeters;
        var nextMinimum = EstimateMinimumFragmentHeight(
            followingBlock,
            flow.ContentWidthMillimeters);
        var combinedHeight = paragraphHeight + nextMinimum;
        if (combinedHeight > flow.RemainingBodyHeightMillimeters
            && combinedHeight <= flow.BodyHeightMillimeters)
        {
            flow.NewPage();
        }
    }

    private double EstimateMinimumFragmentHeight(
        ImportedDocumentBlock block,
        double contentWidthMillimeters) =>
        block switch
        {
            ImportedParagraphBlock paragraph => _measurement.Measure(
                MapRuns(paragraph.Runs),
                contentWidthMillimeters).FirstLineHeightMillimeters,
            ImportedTableBlock table when table.Rows.Count > 0 => _measurement.EstimateTableRowHeight(
                table.Rows[0],
                Math.Max(1, table.Rows[0].Count),
                contentWidthMillimeters,
                bold: table.RepeatFirstRow),
            ImportedImageBlock image => Math.Min(
                image.HeightMillimeters ?? 40d,
                Math.Max(1d, contentWidthMillimeters)),
            ImportedUnsupportedBlock => 12d,
            ImportedExplicitPageBreakBlock => double.MaxValue,
            _ => 8d
        };

    private void LayoutTable(
        LayoutPageFlow flow,
        ImportedTableBlock table,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> headers = [];
        IReadOnlyList<IReadOnlyList<string>> rows = table.Rows;
        if (table.RepeatFirstRow && table.Rows.Count > 0)
        {
            headers = table.Rows[0].ToList();
            rows = table.Rows.Skip(1).ToList();
        }

        _tablePaginator.Layout(
            flow,
            elementId: null,
            name: "Imported table",
            caption: null,
            captionFormat: null,
            columnHeaders: headers,
            rows: rows,
            cellSpans: [],
            rowGroups: [],
            sourceError: null,
            isEditable: false,
            repeatHeader: table.RepeatFirstRow,
            format: DefaultFormatProfiles.Table,
            warnings: warnings,
            cancellationToken: cancellationToken);
    }

    private static void LayoutImage(
        LayoutPageFlow flow,
        ImportedImageBlock image,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = CalculateImageSize(
            image.WidthMillimeters,
            image.HeightMillimeters,
            flow.ContentWidthMillimeters,
            Math.Max(1d, flow.BodyHeightMillimeters));

        if (!flow.IsAtBodyTop && target.HeightMillimeters > flow.RemainingBodyHeightMillimeters)
            flow.NewPage();

        flow.AddBodyBlock(new PositionedPageBlock
        {
            ElementId = null,
            Region = DocumentPageRegion.Body,
            Kind = PageBlockKind.Image,
            XMillimeters = Math.Max(0d, flow.CurrentPage.PageLayout.MarginLeftMillimeters),
            YMillimeters = flow.BodyYMillimeters,
            WidthMillimeters = target.WidthMillimeters,
            HeightMillimeters = target.HeightMillimeters,
            FragmentIndex = 0,
            IsContinuation = false,
            IsEditableReportElement = false,
            Payload = new ImagePageBlockPayload
            {
                Name = image.Name,
                ImageBytes = image.ImageBytes,
                ContentType = image.ContentType,
                IntrinsicWidthMillimeters = image.WidthMillimeters,
                IntrinsicHeightMillimeters = image.HeightMillimeters
            }
        });
    }

    private static ImageSize CalculateImageSize(
        double? intrinsicWidthMillimeters,
        double? intrinsicHeightMillimeters,
        double availableWidthMillimeters,
        double availableHeightMillimeters)
    {
        var width = intrinsicWidthMillimeters is > 0d
            ? intrinsicWidthMillimeters.Value
            : Math.Min(80d, availableWidthMillimeters);
        var height = intrinsicHeightMillimeters is > 0d
            ? intrinsicHeightMillimeters.Value
            : 40d;

        if (intrinsicWidthMillimeters is > 0d && intrinsicHeightMillimeters is > 0d)
        {
            var scale = Math.Min(
                1d,
                Math.Min(availableWidthMillimeters / width, availableHeightMillimeters / height));
            width *= Math.Max(0d, scale);
            height *= Math.Max(0d, scale);
        }
        else
        {
            width = Math.Min(width, availableWidthMillimeters);
            height = Math.Min(height, availableHeightMillimeters);
        }

        return new ImageSize(Math.Max(1d, width), Math.Max(1d, height));
    }

    private static void LayoutUnsupported(
        LayoutPageFlow flow,
        ImportedUnsupportedBlock unsupported,
        List<string> warnings)
    {
        const double desiredHeight = 12d;
        if (!flow.IsAtBodyTop && desiredHeight > flow.RemainingBodyHeightMillimeters)
            flow.NewPage();

        warnings.Add($"İçe aktarılan belge desteklenmeyen içerik barındırıyor: {unsupported.Description}");
        flow.AddBodyBlock(new PositionedPageBlock
        {
            ElementId = null,
            Region = DocumentPageRegion.Body,
            Kind = PageBlockKind.Unsupported,
            XMillimeters = Math.Max(0d, flow.CurrentPage.PageLayout.MarginLeftMillimeters),
            YMillimeters = flow.BodyYMillimeters,
            WidthMillimeters = flow.ContentWidthMillimeters,
            HeightMillimeters = Math.Min(desiredHeight, Math.Max(1d, flow.BodyHeightMillimeters)),
            FragmentIndex = 0,
            IsContinuation = false,
            IsEditableReportElement = false,
            Payload = new UnsupportedPageBlockPayload { Description = unsupported.Description }
        });
    }

    private static IReadOnlyList<MeasuredTextRun> MapRuns(
        IReadOnlyList<ImportedTextRun> runs) =>
        runs.Select(run => new MeasuredTextRun(
            run.Text,
            run.Bold,
            run.Italic,
            run.Underline,
            run.FontSizePoints,
            run.FontFamilyName)).ToList();

    private sealed record ImageSize(double WidthMillimeters, double HeightMillimeters);
}
