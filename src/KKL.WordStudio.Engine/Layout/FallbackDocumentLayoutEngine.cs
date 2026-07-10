namespace KKL.WordStudio.Engine.Layout;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;

/// <summary>
/// Compileable Sprint 14 bootstrap implementation. It deliberately lays the
/// generated semantic document onto one page and does not claim pagination.
/// Team A replaces this behavior behind IDocumentLayoutEngine.
/// </summary>
public sealed class FallbackDocumentLayoutEngine : IDocumentLayoutEngine
{
    public const string FallbackWarning = "Fallback layout active; true pagination is not enabled.";

    public Task<DocumentLayoutResult> LayoutAsync(
        DocumentLayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var document = request.ReportContent;
        var layout = document.PageLayout;
        var blocks = new List<PositionedPageBlock>();

        var contentWidth = Math.Max(
            1d,
            layout.WidthMillimeters - layout.MarginLeftMillimeters - layout.MarginRightMillimeters);

        var headerY = 5d;
        AddGeneratedNodes(
            blocks,
            document.HeaderNodes,
            DocumentPageRegion.Header,
            layout.MarginLeftMillimeters,
            ref headerY,
            contentWidth);

        var bodyY = layout.MarginTopMillimeters;
        if (document.TableOfContents.Count > 0)
        {
            var tocHeight = Math.Max(8d, 5d + (document.TableOfContents.Count * 5d));
            blocks.Add(new PositionedPageBlock
            {
                ElementId = null,
                Region = DocumentPageRegion.Body,
                Kind = PageBlockKind.TableOfContents,
                XMillimeters = layout.MarginLeftMillimeters,
                YMillimeters = bodyY,
                WidthMillimeters = contentWidth,
                HeightMillimeters = tocHeight,
                FragmentIndex = 0,
                IsContinuation = false,
                IsEditableReportElement = false,
                Payload = new TocPageBlockPayload
                {
                    Entries = document.TableOfContents
                        .Select(entry => new LaidOutTocEntry
                        {
                            ElementId = entry.ElementId,
                            Text = entry.Text,
                            Level = entry.Level,
                            PageNumber = 1
                        })
                        .ToList()
                }
            });
            bodyY += tocHeight + 2d;
        }

        AddGeneratedNodes(
            blocks,
            document.BodyNodes,
            DocumentPageRegion.Body,
            layout.MarginLeftMillimeters,
            ref bodyY,
            contentWidth);

        var footerY = Math.Max(
            layout.MarginTopMillimeters,
            layout.HeightMillimeters - layout.MarginBottomMillimeters + 2d);
        AddGeneratedNodes(
            blocks,
            document.FooterNodes,
            DocumentPageRegion.Footer,
            layout.MarginLeftMillimeters,
            ref footerY,
            contentWidth);

        if (layout.ShowPageNumbers)
        {
            blocks.Add(new PositionedPageBlock
            {
                ElementId = null,
                Region = DocumentPageRegion.Footer,
                Kind = PageBlockKind.PageNumber,
                XMillimeters = layout.MarginLeftMillimeters,
                YMillimeters = Math.Max(0d, layout.HeightMillimeters - Math.Max(8d, layout.MarginBottomMillimeters / 2d)),
                WidthMillimeters = contentWidth,
                HeightMillimeters = 5d,
                FragmentIndex = 0,
                IsContinuation = false,
                IsEditableReportElement = false,
                Payload = new PageNumberPageBlockPayload { PageNumber = 1 }
            });
        }

        var warnings = new List<string> { FallbackWarning };
        foreach (var warning in document.FormatWarnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))
        {
            var normalized = warning.Trim();
            if (!warnings.Contains(normalized, StringComparer.Ordinal))
                warnings.Add(normalized);
        }

        if (request.FrontMatter is not null)
            warnings.Add("Imported front matter layout is pending; fallback engine rendered generated report content only.");

        var result = new DocumentLayoutResult
        {
            Pages =
            [
                new DocumentPageLayout
                {
                    PageNumber = 1,
                    Origin = DocumentPageOrigin.GeneratedReport,
                    PageLayout = layout,
                    Blocks = blocks
                }
            ],
            Warnings = warnings
        };

        return Task.FromResult(result);
    }

    private static void AddGeneratedNodes(
        ICollection<PositionedPageBlock> target,
        IReadOnlyList<ReportContentNode> nodes,
        DocumentPageRegion region,
        double xMillimeters,
        ref double yMillimeters,
        double widthMillimeters)
    {
        foreach (var node in nodes)
        {
            var (kind, height, payload) = MapNode(node);
            target.Add(new PositionedPageBlock
            {
                ElementId = node.ElementId,
                Region = region,
                Kind = kind,
                XMillimeters = xMillimeters,
                YMillimeters = yMillimeters,
                WidthMillimeters = widthMillimeters,
                HeightMillimeters = height,
                FragmentIndex = 0,
                IsContinuation = false,
                IsEditableReportElement = true,
                Payload = payload
            });

            yMillimeters += height + 2d;
        }
    }

    private static (PageBlockKind Kind, double Height, PageBlockPayload Payload) MapNode(ReportContentNode node) =>
        node switch
        {
            TextContentNode text => (
                PageBlockKind.Text,
                EstimateTextHeight(text),
                new TextPageBlockPayload
                {
                    Runs =
                    [
                        new TextRunLayout
                        {
                            Text = text.Text,
                            Bold = text.Bold,
                            Italic = false,
                            Underline = false,
                            FontSizePoints = text.FontSize > 0d ? text.FontSize : 11d,
                            FontFamilyName = null
                        }
                    ],
                    SemanticKind = text.Kind,
                    Alignment = text.Format.Alignment,
                    Format = text.Format
                }),
            TableContentNode table => (
                PageBlockKind.Table,
                EstimateTableHeight(table),
                new TablePageBlockPayload
                {
                    Name = table.Name,
                    Caption = table.Caption,
                    CaptionFormat = table.CaptionFormat,
                    ColumnHeaders = table.ColumnHeaders,
                    Rows = table.Rows,
                    CellSpans = table.CellSpans,
                    Format = table.Format,
                    StartRowIndex = 0,
                    HasHeader = table.ColumnHeaders.Count > 0,
                    IsHeaderRepeated = false,
                    SourceError = table.SourceError
                }),
            ImageContentNode image => (
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
            _ => (
                PageBlockKind.Unsupported,
                8d,
                new UnsupportedPageBlockPayload
                {
                    Description = $"Unsupported generated report content node: {node.GetType().Name}"
                })
        };

    private static double EstimateTextHeight(TextContentNode text)
    {
        var lineCount = Math.Max(1, text.Text.Split('\n').Length);
        return Math.Max(6d, lineCount * 6d);
    }

    private static double EstimateTableHeight(TableContentNode table)
    {
        var headerRows = table.ColumnHeaders.Count > 0 ? 1 : 0;
        var captionRows = string.IsNullOrWhiteSpace(table.Caption) ? 0 : 1;
        return Math.Max(10d, (table.Rows.Count + headerRows + captionRows) * 7d);
    }
}
