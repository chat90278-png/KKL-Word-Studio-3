namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Styling;

/// <summary>
/// Resolves supported authored KKL semantics against a normalized document-format
/// profile. Imported reference profiles override the deterministic built-in default.
/// It never opens or interprets a DOCX package.
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

        var effectiveProfile = profile ?? DefaultDocumentFormatProfileFactory.Create();
        var reference = kind switch
        {
            ReportContentKind.Heading => effectiveProfile.PrimaryHeading,
            ReportContentKind.AltHeading => effectiveProfile.SecondaryHeading,
            _ => effectiveProfile.BodyText
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

        var effectiveProfile = profile ?? DefaultDocumentFormatProfileFactory.Create();
        var selected = TableFormatProfileSelector.Select(effectiveProfile, table);
        if (selected is null)
            return compatibilityResolver.ResolveTable(null, table);

        return AutomaticTableFormatWidthAdapter.Adapt(selected, table);
    }

    public PageLayout ResolvePageLayout(
        DocumentFormatProfile? profile,
        PageLayout authoredLayout)
    {
        ArgumentNullException.ThrowIfNull(authoredLayout);
        var effectiveProfile = profile ?? DefaultDocumentFormatProfileFactory.Create();

        return new PageLayout
        {
            WidthMillimeters = effectiveProfile.Page.WidthMillimeters,
            HeightMillimeters = effectiveProfile.Page.HeightMillimeters,
            MarginTopMillimeters = effectiveProfile.Page.MarginTopMillimeters,
            MarginBottomMillimeters = effectiveProfile.Page.MarginBottomMillimeters,
            MarginLeftMillimeters = effectiveProfile.Page.MarginLeftMillimeters,
            MarginRightMillimeters = effectiveProfile.Page.MarginRightMillimeters,
            HeaderDistanceMillimeters = effectiveProfile.Page.HeaderDistanceMillimeters,
            FooterDistanceMillimeters = effectiveProfile.Page.FooterDistanceMillimeters,
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
