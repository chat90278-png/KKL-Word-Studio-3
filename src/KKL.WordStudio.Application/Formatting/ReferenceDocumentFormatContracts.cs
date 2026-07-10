namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Application.Layout;

public sealed class ReferenceDocumentFormatResult
{
    public required DocumentFormatProfile? Profile { get; init; }
    public required bool IsMissing { get; init; }
    public string? StatusMessage { get; init; }
}

public sealed class DocumentFormatProfile
{
    public required PageFormatProfile Page { get; init; }
    public required ResolvedTextFormat PrimaryHeading { get; init; }
    public required ResolvedTextFormat SecondaryHeading { get; init; }
    public required ResolvedTextFormat BodyText { get; init; }
    public required ResolvedTextFormat TableCaption { get; init; }
    public TableCaptionSequenceProfile? TableCaptionSequence { get; init; }
    public required IReadOnlyList<ReferenceTableFormatProfile> TableFormats { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class PageFormatProfile
{
    public required double WidthMillimeters { get; init; }
    public required double HeightMillimeters { get; init; }
    public required double MarginTopMillimeters { get; init; }
    public required double MarginBottomMillimeters { get; init; }
    public required double MarginLeftMillimeters { get; init; }
    public required double MarginRightMillimeters { get; init; }
    public required double HeaderDistanceMillimeters { get; init; }
    public required double FooterDistanceMillimeters { get; init; }
}

public sealed class ResolvedTextFormat
{
    public required string FontFamilyName { get; init; }
    public required double FontSizePoints { get; init; }
    public required bool Bold { get; init; }
    public required bool Italic { get; init; }
    public required bool Underline { get; init; }
    public required string ForegroundColor { get; init; }
    public required ParagraphAlignment Alignment { get; init; }
    public required double SpaceBeforePoints { get; init; }
    public required double SpaceAfterPoints { get; init; }
    public required double LineSpacingMultiple { get; init; }
    public required double LeftIndentMillimeters { get; init; }
    public required double FirstLineIndentMillimeters { get; init; }
    public required bool KeepWithNext { get; init; }
}

public sealed class TableCaptionSequenceProfile
{
    public required string DisplayLabel { get; init; }
    public required string SequenceIdentifier { get; init; }
    public required string Separator { get; init; }
}

public sealed class ReferenceTableFormatProfile
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> ReferenceHeaders { get; init; }
    public required ResolvedTableFormat Format { get; init; }
}

public sealed class ResolvedTableFormat
{
    public required double WidthPercent { get; init; }
    public required bool FixedLayout { get; init; }
    public required double BorderSizePoints { get; init; }
    public required double CellMarginTopMillimeters { get; init; }
    public required double CellMarginBottomMillimeters { get; init; }
    public required double CellMarginLeftMillimeters { get; init; }
    public required double CellMarginRightMillimeters { get; init; }
    public required double PreferredRowHeightMillimeters { get; init; }
    public required bool RepeatHeader { get; init; }
    public required IReadOnlyList<ResolvedTableColumnFormat> Columns { get; init; }
}

public sealed class ResolvedTableColumnFormat
{
    public required double WidthWeight { get; init; }
    public required ParagraphAlignment HeaderAlignment { get; init; }
    public required ParagraphAlignment BodyAlignment { get; init; }
    public required string HeaderFontFamilyName { get; init; }
    public required double HeaderFontSizePoints { get; init; }
    public required bool HeaderBold { get; init; }
    public required string BodyFontFamilyName { get; init; }
    public required double BodyFontSizePoints { get; init; }
    public required bool BodyBold { get; init; }
    public required VerticalContentAlignment VerticalAlignment { get; init; }
    public required bool NoWrap { get; init; }
}

public enum VerticalContentAlignment
{
    Top,
    Center,
    Bottom
}
