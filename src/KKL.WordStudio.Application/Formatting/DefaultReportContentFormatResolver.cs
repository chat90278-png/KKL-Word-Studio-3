namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Styling;

/// <summary>
/// Compatibility resolver for the contract bootstrap. It deliberately preserves
/// authored KKL semantics and does not interpret reference DOCX profiles; Team B
/// supplies the reference-aware production resolver.
/// </summary>
public sealed class DefaultReportContentFormatResolver : IReportContentFormatResolver
{
    public ResolvedTextFormat ResolveText(
        DocumentFormatProfile? profile,
        ReportContentKind kind,
        Style elementStyle)
    {
        ArgumentNullException.ThrowIfNull(elementStyle);

        return new ResolvedTextFormat
        {
            FontFamilyName = string.IsNullOrWhiteSpace(elementStyle.FontFamily) ? "Segoe UI" : elementStyle.FontFamily,
            FontSizePoints = elementStyle.FontSize > 0d ? elementStyle.FontSize : 11d,
            Bold = elementStyle.Bold,
            Italic = elementStyle.Italic,
            Underline = elementStyle.Underline,
            ForegroundColor = string.IsNullOrWhiteSpace(elementStyle.ForegroundColor) ? "#FF000000" : elementStyle.ForegroundColor,
            Alignment = elementStyle.HorizontalAlignment switch
            {
                HorizontalAlignment.Center => ParagraphAlignment.Center,
                HorizontalAlignment.Right => ParagraphAlignment.Right,
                HorizontalAlignment.Justify => ParagraphAlignment.Justify,
                _ => ParagraphAlignment.Left
            },
            SpaceBeforePoints = 0d,
            SpaceAfterPoints = 0d,
            LineSpacingMultiple = 1d,
            LeftIndentMillimeters = 0d,
            FirstLineIndentMillimeters = 0d,
            KeepWithNext = kind is ReportContentKind.Heading or ReportContentKind.AltHeading
        };
    }

    public ResolvedTableFormat ResolveTable(
        DocumentFormatProfile? profile,
        TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var columns = table.Columns.Select(_ => new ResolvedTableColumnFormat
        {
            WidthWeight = 1d,
            HeaderAlignment = ParagraphAlignment.Left,
            BodyAlignment = ParagraphAlignment.Left,
            HeaderFontFamilyName = "Segoe UI",
            HeaderFontSizePoints = 10d,
            HeaderBold = true,
            BodyFontFamilyName = "Segoe UI",
            BodyFontSizePoints = 10d,
            BodyBold = false,
            VerticalAlignment = VerticalContentAlignment.Top,
            NoWrap = false
        }).ToArray();

        return new ResolvedTableFormat
        {
            WidthPercent = 100d,
            FixedLayout = true,
            BorderSizePoints = 0.5d,
            CellMarginTopMillimeters = 0d,
            CellMarginBottomMillimeters = 0d,
            CellMarginLeftMillimeters = 0d,
            CellMarginRightMillimeters = 0d,
            PreferredRowHeightMillimeters = 0d,
            RepeatHeader = true,
            Columns = columns
        };
    }

    public PageLayout ResolvePageLayout(
        DocumentFormatProfile? profile,
        PageLayout authoredLayout)
    {
        ArgumentNullException.ThrowIfNull(authoredLayout);
        return authoredLayout;
    }
}
