namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Application.Layout;

/// <summary>Deterministic compatibility formats used when no reference-format resolver is active.</summary>
public static class DefaultFormatProfiles
{
    public static ResolvedTextFormat BodyText { get; } = new()
    {
        FontFamilyName = "Segoe UI",
        FontSizePoints = 10d,
        Bold = false,
        Italic = false,
        Underline = false,
        ForegroundColor = "#FF000000",
        Alignment = ParagraphAlignment.Left,
        SpaceBeforePoints = 0d,
        SpaceAfterPoints = 0d,
        LineSpacingMultiple = 1d,
        LeftIndentMillimeters = 0d,
        FirstLineIndentMillimeters = 0d,
        KeepWithNext = false
    };

    public static ResolvedTableFormat Table { get; } = new()
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
        Columns = Array.Empty<ResolvedTableColumnFormat>()
    };
}
