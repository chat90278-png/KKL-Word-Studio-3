namespace KKL.WordStudio.Infrastructure.Export.Exporters.Word;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;

/// <summary>
/// Creates the HeaderPart/FooterPart (only when there's actual content or a
/// page number to show) and wires their references into the document's
/// SectionProperties.
///
/// Explicitly calls Header.Save(headerPart) / Footer.Save(footerPart): with
/// autoSave: false on the WordprocessingDocument (WordExporter), each
/// part's root element needs its own explicit Save() or its content may
/// not persist correctly into the final package — a real bug caught during
/// Sprint 5 stabilization, preserved here when this logic moved into its
/// own class.
/// </summary>
internal static class WordHeaderFooterWriter
{
    public static void AppendHeaderFooterReferences(MainDocumentPart mainPart, SectionProperties sectionProperties, ReportContentDocument document)
    {
        if (document.HeaderNodes.Count > 0)
        {
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var header = new Header();
            foreach (var node in document.HeaderNodes)
                WordContentWriter.AppendNode(header, node);
            headerPart.Header = header;
            header.Save(headerPart);

            sectionProperties.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) });
        }

        if (document.FooterNodes.Count > 0 || document.PageLayout.ShowPageNumbers)
        {
            var footerPart = mainPart.AddNewPart<FooterPart>();
            var footer = new Footer();
            foreach (var node in document.FooterNodes)
                WordContentWriter.AppendNode(footer, node);

            if (document.PageLayout.ShowPageNumbers)
                footer.AppendChild(BuildPageNumberParagraph());

            footerPart.Footer = footer;
            footer.Save(footerPart);
            sectionProperties.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(footerPart) });
        }
    }

    /// <summary>A native, updatable Word PAGE field — Word resolves the real number when the document is opened/printed.</summary>
    private static Paragraph BuildPageNumberParagraph()
    {
        var field = new SimpleField(new Run(new Text("1"))) { Instruction = " PAGE " };
        var paragraph = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Right }));
        paragraph.AppendChild(field);
        return paragraph;
    }
}
