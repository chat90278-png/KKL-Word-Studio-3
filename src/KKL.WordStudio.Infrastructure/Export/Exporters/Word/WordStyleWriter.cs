namespace KKL.WordStudio.Infrastructure.Export.Exporters.Word;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

/// <summary>
/// Writes the document's paragraph styles. Split out of WordExporter
/// (Sprint 6) purely for readability/single-responsibility — this is a
/// plain static helper, not an interface-based abstraction, since there is
/// exactly one way to build these styles today and no reason to swap
/// implementations (see ADR 0008: "small classes, not premature Strategy").
///
/// Explicitly calls Styles.Save(stylesPart): with autoSave: false on the
/// WordprocessingDocument (WordExporter), every part's root element needs
/// its own explicit Save() or its content may not persist correctly into
/// the final package — a real bug caught during Sprint 5 stabilization,
/// preserved here when the styles-writing logic moved into this class.
/// </summary>
internal static class WordStyleWriter
{
    /// <summary>Heading1/Heading2 carry a real outlineLvl — required for Word's native TOC field to find them (see ADR 0007).</summary>
    public static void AddStyleDefinitions(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        styles.Append(new Style
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true,
            StyleName = new StyleName { Val = "Normal" }
        });

        styles.Append(BuildHeadingStyle("Heading1", "heading 1", outlineLevel: 0, fontSizeHalfPoints: "36"));
        styles.Append(BuildHeadingStyle("Heading2", "heading 2", outlineLevel: 1, fontSizeHalfPoints: "28"));

        stylesPart.Styles = styles;
        styles.Save(stylesPart);
    }

    private static Style BuildHeadingStyle(string styleId, string name, int outlineLevel, string fontSizeHalfPoints) => new()
    {
        Type = StyleValues.Paragraph,
        StyleId = styleId,
        StyleName = new StyleName { Val = name },
        BasedOn = new BasedOn { Val = "Normal" },
        StyleParagraphProperties = new StyleParagraphProperties(new OutlineLevel { Val = outlineLevel }),
        StyleRunProperties = new StyleRunProperties(new Bold(), new FontSize { Val = fontSizeHalfPoints })
    };
}
