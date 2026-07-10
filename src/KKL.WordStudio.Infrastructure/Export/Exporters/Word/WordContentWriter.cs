namespace KKL.WordStudio.Infrastructure.Export.Exporters.Word;

using DocumentFormat.OpenXml;
using KKL.WordStudio.Application.Content;

/// <summary>Appends a single ReportContentNode into any composite container (Body, Header, or Footer) — the one place that dispatches by node type, reused by all three regions so they can never diverge in how they render the same node kinds.</summary>
internal static class WordContentWriter
{
    public static void AppendNode(OpenXmlCompositeElement container, ReportContentNode node)
    {
        switch (node)
        {
            case TextContentNode text:
                container.AppendChild(WordParagraphWriter.BuildParagraph(text));
                break;
            case TableContentNode table:
                if (!string.IsNullOrWhiteSpace(table.Caption))
                {
                    container.AppendChild(WordParagraphWriter.BuildTableCaptionParagraph(
                        table.Caption,
                        table.CaptionSequence,
                        table.CaptionFormat));
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
