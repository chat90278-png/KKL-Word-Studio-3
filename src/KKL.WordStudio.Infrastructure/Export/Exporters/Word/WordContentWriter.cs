namespace KKL.WordStudio.Infrastructure.Export.Exporters.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;

/// <summary>Appends a single ReportContentNode into any composite container (Body, Header, or Footer) — the one place that dispatches by node type, reused by all three regions so they can never diverge in how they render the same node kinds.</summary>
internal static class WordContentWriter
{
    public static void AppendNode(OpenXmlCompositeElement container, ReportContentNode node) =>
        AppendNode(container, node, captionSequenceCounters: null, startOnNewPage: false);

    public static void AppendNode(
        OpenXmlCompositeElement container,
        ReportContentNode node,
        IDictionary<string, int>? captionSequenceCounters) =>
        AppendNode(container, node, captionSequenceCounters, startOnNewPage: false);

    public static void AppendNode(
        OpenXmlCompositeElement container,
        ReportContentNode node,
        IDictionary<string, int>? captionSequenceCounters,
        bool startOnNewPage)
    {
        switch (node)
        {
            case TextContentNode text:
            {
                var paragraph = WordParagraphWriter.BuildParagraph(text);
                if (startOnNewPage)
                {
                    paragraph.ParagraphProperties ??= new ParagraphProperties();
                    if (paragraph.ParagraphProperties.GetFirstChild<PageBreakBefore>() is null)
                        paragraph.ParagraphProperties.AddChild(new PageBreakBefore(), true);
                }
                container.AppendChild(paragraph);
                break;
            }
            case TableContentNode table:
                if (!string.IsNullOrWhiteSpace(table.Caption))
                {
                    var sequenceNumber = captionSequenceCounters is null
                        ? null
                        : TableCaptionSequenceFormatter.ResolveNextSequenceNumber(
                            table.Caption,
                            table.CaptionSequence,
                            captionSequenceCounters);
                    container.AppendChild(WordParagraphWriter.BuildTableCaptionParagraph(
                        table.Caption,
                        table.CaptionSequence,
                        table.CaptionFormat,
                        sequenceNumber));
                }
                if (table.Rows.Count == 0 && table.ColumnHeaders.Count == 0)
                    break; // caption may still be meaningful even before columns are configured
                container.AppendChild(WordTableWriter.BuildTable(table));
                break;
            case ImageContentNode image:
                // Real image embedding needs the Asset/resource catalog (deliberately
                // deferred — ADR 0004). A text placeholder keeps the document's
                // structure correct without silently dropping the element.
                container.AppendChild(WordParagraphWriter.BuildPlainParagraph($"[Image: {image.Name}]"));
                break;
        }
    }
}
