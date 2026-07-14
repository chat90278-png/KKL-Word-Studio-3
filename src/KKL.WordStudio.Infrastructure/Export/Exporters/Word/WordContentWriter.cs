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
                    var captionParagraph = WordParagraphWriter.BuildTableCaptionParagraph(
                        table.Caption,
                        table.CaptionSequence,
                        table.CaptionFormat,
                        sequenceNumber);
                    if (ReportFlowPaginationPolicy.KeepTableCaptionWithTable(table.Caption))
                    {
                        captionParagraph.ParagraphProperties ??= new ParagraphProperties();
                        if (captionParagraph.ParagraphProperties.GetFirstChild<KeepNext>() is null)
                            captionParagraph.ParagraphProperties.AddChild(new KeepNext(), true);
                    }
                    container.AppendChild(captionParagraph);
                }
                if (table.Rows.Count == 0 && table.ColumnHeaders.Count == 0)
                    break; // caption may still be meaningful even before columns are configured

                var wordTable = WordTableWriter.BuildTable(table);
                ApplyTablePaginationPolicy(wordTable);
                container.AppendChild(wordTable);
                break;
            case ImageContentNode image:
                // Real image embedding needs the Asset/resource catalog (deliberately
                // deferred — ADR 0004). A text placeholder keeps the document's
                // structure correct without silently dropping the element.
                container.AppendChild(WordParagraphWriter.BuildPlainParagraph($"[Image: {image.Name}]"));
                break;
        }
    }

    private static void ApplyTablePaginationPolicy(Table table)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (ReportFlowPaginationPolicy.KeepTableRowsIntact)
        {
            foreach (var row in rows)
            {
                row.TableRowProperties ??= new TableRowProperties();
                if (row.TableRowProperties.GetFirstChild<CantSplit>() is null)
                    row.TableRowProperties.AddChild(new CantSplit(), true);
            }
        }

        if (rows.Count == 0)
            return;

        var hasHeader = rows[0].TableRowProperties?.GetFirstChild<TableHeader>() is not null;
        var dataRowCount = Math.Max(0, rows.Count - (hasHeader ? 1 : 0));
        var requiredDataRows = ReportFlowPaginationPolicy.ResolveMinimumTableStartDataRowCount(dataRowCount);
        var requiredStartRowCount = requiredDataRows + (hasHeader ? 1 : 0);

        // Native Word pagination has no explicit table-fragment object. KeepNext
        // on the header and leading data-row paragraphs expresses the same shared
        // start requirement without splitting or rebuilding the semantic table.
        for (var rowIndex = 0; rowIndex < requiredStartRowCount - 1; rowIndex++)
        {
            foreach (var paragraph in rows[rowIndex].Descendants<Paragraph>())
            {
                paragraph.ParagraphProperties ??= new ParagraphProperties();
                if (paragraph.ParagraphProperties.GetFirstChild<KeepNext>() is null)
                    paragraph.ParagraphProperties.AddChild(new KeepNext(), true);
            }
        }
    }
}
