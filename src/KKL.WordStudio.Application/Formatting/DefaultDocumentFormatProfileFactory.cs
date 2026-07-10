namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Application.Layout;

/// <summary>
/// Deterministic built-in formatting baseline for generated KKL documents.
/// Imported project reference formats may replace this profile, while
/// Preview/Engine/Word consumers continue to receive only the shared
/// resolved-format contracts.
/// </summary>
public static class DefaultDocumentFormatProfileFactory
{
    public static DocumentFormatProfile Create() => new()
    {
        Page = new PageFormatProfile
        {
            WidthMillimeters = 210.009d,
            HeightMillimeters = 297.004d,
            MarginTopMillimeters = 24.994d,
            MarginBottomMillimeters = 24.994d,
            MarginLeftMillimeters = 24.994d,
            MarginRightMillimeters = 24.994d,
            HeaderDistanceMillimeters = 12.488d,
            FooterDistanceMillimeters = 12.488d
        },
        PrimaryHeading = TextFormat(
            size: 12d,
            bold: true,
            italic: true,
            spaceBefore: 4d,
            spaceAfter: 2d,
            keepWithNext: true),
        SecondaryHeading = TextFormat(
            size: 12d,
            bold: true,
            leftIndentMillimeters: 21.75d,
            keepWithNext: true),
        BodyText = TextFormat(size: 10d),
        TableCaption = TextFormat(
            size: 8d,
            bold: true,
            alignment: ParagraphAlignment.Center,
            lineSpacingMultiple: 2d,
            keepWithNext: true),
        TableCaptionSequence = new TableCaptionSequenceProfile
        {
            DisplayLabel = "Tablo",
            SequenceIdentifier = "Tablo",
            Separator = ". "
        },
        TableFormats =
        [
            CreateTableProfile(
                "built-in-default-table-1",
                "Default Table Format 1",
                [5.13d, 30.04d, 15.49d, 18.33d, 21.08d, 9.93d]),
            CreateTableProfile(
                "built-in-default-table-2",
                "Default Table Format 2",
                [5.21d, 28.33d, 17.54d, 17.54d, 20.02d, 11.34d])
        ],
        Warnings = []
    };

    private static ResolvedTextFormat TextFormat(
        double size,
        bool bold = false,
        bool italic = false,
        ParagraphAlignment alignment = ParagraphAlignment.Left,
        double spaceBefore = 0d,
        double spaceAfter = 0d,
        double lineSpacingMultiple = 1d,
        double leftIndentMillimeters = 0d,
        bool keepWithNext = false) => new()
    {
        FontFamilyName = "Arial",
        FontSizePoints = size,
        Bold = bold,
        Italic = italic,
        Underline = false,
        ForegroundColor = "#FF000000",
        Alignment = alignment,
        SpaceBeforePoints = spaceBefore,
        SpaceAfterPoints = spaceAfter,
        LineSpacingMultiple = lineSpacingMultiple,
        LeftIndentMillimeters = leftIndentMillimeters,
        FirstLineIndentMillimeters = 0d,
        KeepWithNext = keepWithNext
    };

    private static ReferenceTableFormatProfile CreateTableProfile(
        string key,
        string displayName,
        IReadOnlyList<double> weights)
    {
        var columns = weights
            .Select((weight, index) => new ResolvedTableColumnFormat
            {
                WidthWeight = weight,
                HeaderAlignment = index == 1 ? ParagraphAlignment.Left : ParagraphAlignment.Center,
                BodyAlignment = index == 1 ? ParagraphAlignment.Left : ParagraphAlignment.Center,
                HeaderFontFamilyName = "Arial",
                HeaderFontSizePoints = 10d,
                HeaderBold = true,
                BodyFontFamilyName = "Arial",
                BodyFontSizePoints = 10d,
                BodyBold = false,
                VerticalAlignment = VerticalContentAlignment.Center,
                NoWrap = false
            })
            .ToArray();

        return new ReferenceTableFormatProfile
        {
            Key = key,
            DisplayName = displayName,
            ReferenceHeaders = ["Column 1", "Column 2", "Column 3", "Column 4", "Column 5", "Column 6"],
            Format = new ResolvedTableFormat
            {
                WidthPercent = 100d,
                FixedLayout = true,
                BorderSizePoints = 0.5d,
                CellMarginTopMillimeters = 0d,
                CellMarginBottomMillimeters = 0d,
                CellMarginLeftMillimeters = 1.235d,
                CellMarginRightMillimeters = 1.235d,
                PreferredRowHeightMillimeters = 10.195d,
                RepeatHeader = true,
                Columns = columns
            }
        };
    }
}
