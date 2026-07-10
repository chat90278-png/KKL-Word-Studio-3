namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Styling;

/// <summary>
/// Resolves supported authored KKL semantics against an optional normalized
/// reference-DOCX format profile. It never opens or interprets a DOCX package.
/// </summary>
public sealed class ReferenceReportContentFormatResolver : IReportContentFormatResolver
{
    private const string DefaultFontFamily = "Segoe UI";

    private readonly DefaultReportContentFormatResolver compatibilityResolver = new();

    public ResolvedTextFormat ResolveText(
        DocumentFormatProfile? profile,
        ReportContentKind kind,
        Style elementStyle)
    {
        ArgumentNullException.ThrowIfNull(elementStyle);

        if (profile is null)
            return compatibilityResolver.ResolveText(null, kind, elementStyle);

        var reference = kind switch
        {
            ReportContentKind.Heading => profile.PrimaryHeading,
            ReportContentKind.AltHeading => profile.SecondaryHeading,
            _ => profile.BodyText
        };
        var semanticDefault = SemanticDefaultFor(kind);

        return new ResolvedTextFormat
        {
            FontFamilyName = IsExplicitFontFamily(elementStyle.FontFamily, semanticDefault.FontFamily)
                ? elementStyle.FontFamily
                : reference.FontFamilyName,
            FontSizePoints = IsExplicitNumber(elementStyle.FontSize, semanticDefault.FontSize)
                ? elementStyle.FontSize
                : reference.FontSizePoints,
            Bold = IsExplicitBoolean(elementStyle.Bold, semanticDefault.Bold) ? elementStyle.Bold : reference.Bold,
            Italic = IsExplicitBoolean(elementStyle.Italic, semanticDefault.Italic) ? elementStyle.Italic : reference.Italic,
            Underline = IsExplicitBoolean(elementStyle.Underline, semanticDefault.Underline) ? elementStyle.Underline : reference.Underline,
            ForegroundColor = reference.ForegroundColor,
            Alignment = elementStyle.HorizontalAlignment == semanticDefault.HorizontalAlignment
                ? reference.Alignment
                : MapAlignment(elementStyle.HorizontalAlignment),
            SpaceBeforePoints = reference.SpaceBeforePoints,
            SpaceAfterPoints = reference.SpaceAfterPoints,
            LineSpacingMultiple = reference.LineSpacingMultiple,
            LeftIndentMillimeters = reference.LeftIndentMillimeters,
            FirstLineIndentMillimeters = reference.FirstLineIndentMillimeters,
            KeepWithNext = reference.KeepWithNext
        };
    }

    public ResolvedTableFormat ResolveTable(
        DocumentFormatProfile? profile,
        TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (profile is null || profile.TableFormats.Count == 0)
            return compatibilityResolver.ResolveTable(null, table);

        ReferenceTableFormatProfile? selected = null;
        if (!string.IsNullOrWhiteSpace(table.ReferenceTableFormatKey))
        {
            selected = profile.TableFormats.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, table.ReferenceTableFormatKey, StringComparison.Ordinal));
        }

        selected ??= profile.TableFormats.FirstOrDefault(candidate =>
            candidate.ReferenceHeaders.Count == table.Columns.Count);
        selected ??= profile.TableFormats.FirstOrDefault();

        return selected?.Format ?? compatibilityResolver.ResolveTable(null, table);
    }

    public PageLayout ResolvePageLayout(
        DocumentFormatProfile? profile,
        PageLayout authoredLayout)
    {
        ArgumentNullException.ThrowIfNull(authoredLayout);
        if (profile is null)
            return authoredLayout;

        return new PageLayout
        {
            WidthMillimeters = profile.Page.WidthMillimeters,
            HeightMillimeters = profile.Page.HeightMillimeters,
            MarginTopMillimeters = profile.Page.MarginTopMillimeters,
            MarginBottomMillimeters = profile.Page.MarginBottomMillimeters,
            MarginLeftMillimeters = profile.Page.MarginLeftMillimeters,
            MarginRightMillimeters = profile.Page.MarginRightMillimeters,
            HeaderDistanceMillimeters = profile.Page.HeaderDistanceMillimeters,
            FooterDistanceMillimeters = profile.Page.FooterDistanceMillimeters,
            ShowPageNumbers = authoredLayout.ShowPageNumbers
        };
    }

    private static Style SemanticDefaultFor(ReportContentKind kind) => kind switch
    {
        ReportContentKind.Heading => HeadingStylePresets.CreateHeadingStyle(),
        ReportContentKind.AltHeading => HeadingStylePresets.CreateAltHeadingStyle(),
        _ => new Style()
    };

    private static bool IsExplicitFontFamily(string? value, string semanticDefault) =>
        !string.IsNullOrWhiteSpace(value)
        && !string.Equals(value.Trim(), semanticDefault, StringComparison.OrdinalIgnoreCase);

    private static bool IsExplicitNumber(double value, double semanticDefault) =>
        value > 0d && Math.Abs(value - semanticDefault) > 0.0001d;

    private static bool IsExplicitBoolean(bool value, bool semanticDefault) => value != semanticDefault;

    private static ParagraphAlignment MapAlignment(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => ParagraphAlignment.Center,
        HorizontalAlignment.Right => ParagraphAlignment.Right,
        HorizontalAlignment.Justify => ParagraphAlignment.Justify,
        _ => ParagraphAlignment.Left
    };
}
