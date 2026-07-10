namespace KKL.WordStudio.Infrastructure.Export.Exporters.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;

/// <summary>Writes ordinary paragraphs — headings, alt headings, body text, captions, and the TOC field paragraph.</summary>
internal static class WordParagraphWriter
{
    private const double TwipsPerMillimeter = 1440.0 / 25.4;

    public static Paragraph BuildParagraph(TextContentNode text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var isHeading = text.Kind is ReportContentKind.Heading or ReportContentKind.AltHeading;
        var format = text.Format;
        var usesCompatibilityDefault = ReferenceEquals(format, DefaultFormatProfiles.BodyText);
        var fontSizePoints = usesCompatibilityDefault && text.FontSize > 0d
            ? text.FontSize
            : format.FontSizePoints;
        var bold = usesCompatibilityDefault
            ? text.Bold || isHeading
            : format.Bold;

        var paragraphProperties = BuildParagraphProperties(format);
        if (isHeading)
        {
            var styleId = text.Kind == ReportContentKind.Heading ? "Heading1" : "Heading2";
            paragraphProperties.AddChild(new ParagraphStyleId { Val = styleId }, true);
        }

        // Historical compatibility: only untouched compatibility-default headings inherit
        // the legacy keep-next behavior. Real resolved formats remain authoritative.
        var keepWithNext = format.KeepWithNext
            || (usesCompatibilityDefault && isHeading);
        if (keepWithNext && paragraphProperties.GetFirstChild<KeepNext>() is null)
            paragraphProperties.AddChild(new KeepNext(), true);

        var paragraph = new Paragraph(paragraphProperties);
        paragraph.AppendChild(new Run(
            BuildRunProperties(
                format.FontFamilyName,
                fontSizePoints,
                bold,
                format.Italic,
                format.Underline,
                format.ForegroundColor),
            new Text(text.Text) { Space = SpaceProcessingModeValues.Preserve }));
        return paragraph;
    }

    public static Paragraph BuildPlainParagraph(string text) => new(new Run(new Text(text)));

    public static Paragraph BuildTableCaptionParagraph(
        string caption,
        TableCaptionSequenceProfile? sequence) =>
        BuildTableCaptionParagraph(caption, sequence, captionFormat: null);

    public static Paragraph BuildTableCaptionParagraph(
        string caption,
        TableCaptionSequenceProfile? sequence,
        ResolvedTextFormat? captionFormat)
    {
        if (captionFormat is null)
        {
            var legacyParagraph = new Paragraph();
            legacyParagraph.AppendChild(new Run(
                new RunProperties(new Bold()),
                new Text(sequence is null ? caption : sequence.DisplayLabel + " ")
                {
                    Space = SpaceProcessingModeValues.Preserve
                }));

            if (sequence is null)
                return legacyParagraph;

            var legacyDescription = RemoveDeterministicManualSequencePrefix(caption, sequence);
            var legacyField = new SimpleField(new Run(new Text("1")))
            {
                Instruction = $" SEQ {sequence.SequenceIdentifier} \\* ARABIC "
            };
            legacyParagraph.AppendChild(legacyField);
            legacyParagraph.AppendChild(new Run(
                new RunProperties(new Bold()),
                new Text(sequence.Separator + legacyDescription)
                {
                    Space = SpaceProcessingModeValues.Preserve
                }));
            return legacyParagraph;
        }

        var paragraph = new Paragraph(BuildParagraphProperties(captionFormat));
        if (sequence is null)
        {
            paragraph.AppendChild(BuildCaptionRun(caption, captionFormat));
            return paragraph;
        }

        paragraph.AppendChild(BuildCaptionRun(sequence.DisplayLabel + " ", captionFormat));
        var descriptiveCaption = RemoveDeterministicManualSequencePrefix(caption, sequence);
        var field = new SimpleField(BuildCaptionRun("1", captionFormat))
        {
            Instruction = $" SEQ {sequence.SequenceIdentifier} \\* ARABIC "
        };
        paragraph.AppendChild(field);
        paragraph.AppendChild(BuildCaptionRun(sequence.Separator + descriptiveCaption, captionFormat));
        return paragraph;
    }


    private static Run BuildCaptionRun(string text, ResolvedTextFormat format) =>
        new(
            BuildRunProperties(
                format.FontFamilyName,
                format.FontSizePoints,
                format.Bold,
                format.Italic,
                format.Underline,
                format.ForegroundColor),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    public static Paragraph BuildPageBreakParagraph() =>
        new(new Run(new Break { Type = BreakValues.Page }));

    /// <summary>A native, updatable Word TOC field — the user "Update Field"s it in Word to populate entries from the Heading1/Heading2-styled paragraphs WordStyleWriter/this class produce.</summary>
    public static Paragraph BuildTocParagraph()
    {
        var field = new SimpleField(new Run(new Text("Right-click and choose \"Update Field\" to generate the table of contents.")))
        {
            Instruction = @" TOC \o ""1-2"" \h \z \u "
        };
        var paragraph = new Paragraph();
        paragraph.AppendChild(field);
        return paragraph;
    }

    private static ParagraphProperties BuildParagraphProperties(ResolvedTextFormat format)
    {
        var properties = new ParagraphProperties();
        if (format.KeepWithNext)
            properties.AddChild(new KeepNext(), true);
        properties.AddChild(new SpacingBetweenLines
        {
            Before = ToTwentiethPoints(format.SpaceBeforePoints),
            After = ToTwentiethPoints(format.SpaceAfterPoints),
            Line = Math.Max(1, (int)Math.Round(format.LineSpacingMultiple * 240d)).ToString(),
            LineRule = LineSpacingRuleValues.Auto
        }, true);
        properties.AddChild(new Indentation
        {
            Left = ToTwips(format.LeftIndentMillimeters),
            FirstLine = ToTwips(format.FirstLineIndentMillimeters)
        }, true);
        properties.AddChild(new Justification { Val = ToJustification(format.Alignment) }, true);
        return properties;
    }

    internal static RunProperties BuildRunProperties(
        string fontFamilyName,
        double fontSizePoints,
        bool bold,
        bool italic,
        bool underline,
        string foregroundColor)
    {
        var halfPoints = Math.Max(1, (int)Math.Round(fontSizePoints * 2d)).ToString();
        var properties = new RunProperties();
        properties.AddChild(new RunFonts
        {
            Ascii = fontFamilyName,
            HighAnsi = fontFamilyName
        }, true);
        properties.AddChild(new Bold { Val = bold }, true);
        properties.AddChild(new Italic { Val = italic }, true);
        properties.AddChild(new Color { Val = NormalizeWordColor(foregroundColor) }, true);
        properties.AddChild(new FontSize { Val = halfPoints }, true);
        properties.AddChild(new Underline
        {
            Val = underline ? UnderlineValues.Single : UnderlineValues.None
        }, true);
        return properties;
    }

    internal static JustificationValues ToJustification(ParagraphAlignment alignment) => alignment switch
    {
        ParagraphAlignment.Center => JustificationValues.Center,
        ParagraphAlignment.Right => JustificationValues.Right,
        ParagraphAlignment.Justify => JustificationValues.Both,
        _ => JustificationValues.Left
    };

    private static string ToTwentiethPoints(double points) =>
        Math.Max(0, (int)Math.Round(points * 20d)).ToString();

    private static string ToTwips(double millimeters) =>
        Math.Max(0, (int)Math.Round(millimeters * TwipsPerMillimeter)).ToString();

    private static string NormalizeWordColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return "000000";

        var normalized = color.Trim().TrimStart('#');
        if (normalized.Length == 8)
            normalized = normalized[2..];

        return normalized.Length == 6 && normalized.All(Uri.IsHexDigit)
            ? normalized.ToUpperInvariant()
            : "000000";
    }

    /// <summary>
    /// Avoids duplicate numbering only for the exact deterministic shape
    /// "{DisplayLabel} {positive integer}{Separator}{description}" at the start
    /// of the authored caption. Digits elsewhere are never interpreted as numbering.
    /// </summary>
    private static string RemoveDeterministicManualSequencePrefix(
        string caption,
        TableCaptionSequenceProfile sequence)
    {
        var prefix = sequence.DisplayLabel + " ";
        if (!caption.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return caption;

        var numberStart = prefix.Length;
        var numberEnd = numberStart;
        while (numberEnd < caption.Length && char.IsAsciiDigit(caption[numberEnd]))
            numberEnd++;

        if (numberEnd == numberStart
            || !caption.AsSpan(numberEnd).StartsWith(sequence.Separator.AsSpan(), StringComparison.Ordinal))
        {
            return caption;
        }

        return caption[(numberEnd + sequence.Separator.Length)..];
    }
}
