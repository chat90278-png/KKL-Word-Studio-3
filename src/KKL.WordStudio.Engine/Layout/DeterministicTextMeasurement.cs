namespace KKL.WordStudio.Engine.Layout;

using KKL.WordStudio.Application.Formatting;

internal sealed class DeterministicTextMeasurement
{
    private const double MillimetersPerPoint = 25.4d / 72d;
    private const double DefaultCharacterWidthFactor = 0.52d;
    private const double LineHeightFactor = 1.25d;
    private const double MinimumLineHeightMillimeters = 3.5d;

    public TextMeasurement Measure(
        IReadOnlyList<MeasuredTextRun> runs,
        double availableWidthMillimeters) =>
        MeasureCore(
            runs,
            availableWidthMillimeters,
            availableWidthMillimeters,
            lineSpacingMultiple: 1d,
            noWrap: false);

    public TextMeasurement MeasureResolvedText(
        string? text,
        ResolvedTextFormat format,
        double availableWidthMillimeters)
    {
        ArgumentNullException.ThrowIfNull(format);

        var leftIndent = Math.Max(0d, format.LeftIndentMillimeters);
        var paragraphWidth = Math.Max(1d, availableWidthMillimeters - leftIndent);
        var firstLineWidth = Math.Max(1d, paragraphWidth - format.FirstLineIndentMillimeters);

        return MeasureCore(
        [
            new MeasuredTextRun(
                text ?? string.Empty,
                format.Bold,
                format.Italic,
                format.Underline,
                NormalizeFontSize(format.FontSizePoints),
                format.FontFamilyName)
        ],
        firstLineWidth,
        paragraphWidth,
        NormalizeLineSpacing(format.LineSpacingMultiple),
        noWrap: false);
    }

    public double EstimatePlainTextHeight(
        string? text,
        double fontSizePoints,
        double availableWidthMillimeters,
        bool bold = false,
        string? fontFamilyName = null,
        double lineSpacingMultiple = 1d,
        bool noWrap = false)
    {
        var measurement = MeasureCore(
        [
            new MeasuredTextRun(
                text ?? string.Empty,
                bold,
                false,
                false,
                NormalizeFontSize(fontSizePoints),
                fontFamilyName)
        ],
        availableWidthMillimeters,
        availableWidthMillimeters,
        NormalizeLineSpacing(lineSpacingMultiple),
        noWrap);

        return measurement.TotalHeightMillimeters;
    }

    public double EstimateTableRowHeight(
        IReadOnlyList<string> cells,
        int columnCount,
        double tableWidthMillimeters,
        bool bold = false)
    {
        var normalizedColumnCount = Math.Max(1, columnCount);
        var cellWidth = Math.Max(1d, tableWidthMillimeters / normalizedColumnCount);
        var maxTextHeight = MinimumLineHeightMillimeters;

        for (var columnIndex = 0; columnIndex < normalizedColumnCount; columnIndex++)
        {
            var text = columnIndex < cells.Count ? cells[columnIndex] : string.Empty;
            maxTextHeight = Math.Max(
                maxTextHeight,
                EstimatePlainTextHeight(text, 10d, Math.Max(1d, cellWidth - 3d), bold));
        }

        return maxTextHeight + 2.5d;
    }

    public static double PointsToMillimeters(double points) =>
        Math.Max(0d, points) * MillimetersPerPoint;

    private static TextMeasurement MeasureCore(
        IReadOnlyList<MeasuredTextRun> runs,
        double firstLineWidthMillimeters,
        double followingLineWidthMillimeters,
        double lineSpacingMultiple,
        bool noWrap)
    {
        var firstWidth = Math.Max(1d, firstLineWidthMillimeters);
        var followingWidth = Math.Max(1d, followingLineWidthMillimeters);
        var characters = ExpandCharacters(runs);
        var lines = WrapCharacters(
            characters,
            firstWidth,
            followingWidth,
            lineSpacingMultiple,
            noWrap);
        return new TextMeasurement(lines);
    }

    private static IReadOnlyList<MeasuredCharacter> ExpandCharacters(IReadOnlyList<MeasuredTextRun> runs)
    {
        var characters = new List<MeasuredCharacter>();
        foreach (var run in runs)
        {
            var style = new MeasuredTextStyle(
                run.Bold,
                run.Italic,
                run.Underline,
                NormalizeFontSize(run.FontSizePoints),
                run.FontFamilyName);

            foreach (var character in run.Text ?? string.Empty)
                characters.Add(new MeasuredCharacter(character, style));
        }

        if (characters.Count == 0)
        {
            characters.Add(new MeasuredCharacter(
                '\0',
                new MeasuredTextStyle(false, false, false, 11d, null)));
        }

        return characters;
    }

    private static IReadOnlyList<MeasuredLine> WrapCharacters(
        IReadOnlyList<MeasuredCharacter> characters,
        double firstLineWidthMillimeters,
        double followingLineWidthMillimeters,
        double lineSpacingMultiple,
        bool noWrap)
    {
        var lines = new List<MeasuredLine>();
        var current = new List<MeasuredCharacter>();
        var currentWidth = 0d;

        foreach (var character in characters)
        {
            if (character.Value == '\0')
            {
                if (current.Count == 0)
                    lines.Add(CreateLine([], false, lineSpacingMultiple));
                continue;
            }

            if (character.Value == '\r')
                continue;

            if (character.Value == '\n')
            {
                lines.Add(CreateLine(current, true, lineSpacingMultiple));
                current = [];
                currentWidth = 0d;
                continue;
            }

            var lineWidth = lines.Count == 0
                ? firstLineWidthMillimeters
                : followingLineWidthMillimeters;
            var characterWidth = EstimateCharacterWidth(character);
            if (!noWrap
                && current.Count > 0
                && currentWidth + characterWidth > lineWidth)
            {
                var whitespaceIndex = FindLastWhitespace(current);
                if (whitespaceIndex >= 0)
                {
                    var lineCharacters = current.Take(whitespaceIndex + 1).ToList();
                    var carry = current.Skip(whitespaceIndex + 1).ToList();
                    lines.Add(CreateLine(lineCharacters, false, lineSpacingMultiple));
                    current = carry;
                    currentWidth = current.Sum(EstimateCharacterWidth);
                }
                else
                {
                    lines.Add(CreateLine(current, false, lineSpacingMultiple));
                    current = [];
                    currentWidth = 0d;
                }
            }

            current.Add(character);
            currentWidth += characterWidth;
        }

        if (current.Count > 0 || lines.Count == 0)
            lines.Add(CreateLine(current, false, lineSpacingMultiple));

        return lines;
    }

    private static int FindLastWhitespace(IReadOnlyList<MeasuredCharacter> characters)
    {
        for (var index = characters.Count - 1; index >= 0; index--)
        {
            if (char.IsWhiteSpace(characters[index].Value))
                return index;
        }

        return -1;
    }

    private static MeasuredLine CreateLine(
        IReadOnlyList<MeasuredCharacter> characters,
        bool endsWithExplicitNewline,
        double lineSpacingMultiple)
    {
        var styles = characters.Select(character => character.Style).ToList();
        var maxFontSize = styles.Count == 0 ? 11d : styles.Max(style => style.FontSizePoints);
        var baseHeight = Math.Max(
            MinimumLineHeightMillimeters,
            maxFontSize * MillimetersPerPoint * LineHeightFactor);
        var height = baseHeight * NormalizeLineSpacing(lineSpacingMultiple);

        return new MeasuredLine(characters.ToList(), height, endsWithExplicitNewline);
    }

    private static double EstimateCharacterWidth(MeasuredCharacter character)
    {
        if (character.Value == '\t')
            return EstimateBaseCharacterWidth(character.Style) * 4d;

        if (char.IsWhiteSpace(character.Value))
            return EstimateBaseCharacterWidth(character.Style) * 0.65d;

        var width = EstimateBaseCharacterWidth(character.Style);
        if (char.IsUpper(character.Value))
            width *= 1.08d;
        else if (char.IsPunctuation(character.Value))
            width *= 0.72d;

        return width;
    }

    private static double EstimateBaseCharacterWidth(MeasuredTextStyle style)
    {
        var boldFactor = style.Bold ? 1.05d : 1d;
        var familyFactor = ResolveFontFamilyWidthFactor(style.FontFamilyName);
        return style.FontSizePoints * MillimetersPerPoint * familyFactor * boldFactor;
    }

    private static double ResolveFontFamilyWidthFactor(string? fontFamilyName)
    {
        if (string.IsNullOrWhiteSpace(fontFamilyName))
            return DefaultCharacterWidthFactor;

        var normalized = fontFamilyName.Trim().ToUpperInvariant();
        if (normalized.Contains("CONSOLAS", StringComparison.Ordinal)
            || normalized.Contains("COURIER", StringComparison.Ordinal)
            || normalized.Contains("MONO", StringComparison.Ordinal))
        {
            return 0.60d;
        }

        if (normalized.Contains("ARIAL", StringComparison.Ordinal)
            || normalized.Contains("HELVETICA", StringComparison.Ordinal))
        {
            return 0.50d;
        }

        if (normalized.Contains("TIMES", StringComparison.Ordinal)
            || normalized.Contains("SERIF", StringComparison.Ordinal))
        {
            return 0.49d;
        }

        return DefaultCharacterWidthFactor;
    }

    private static double NormalizeFontSize(double fontSizePoints) =>
        fontSizePoints > 0d ? fontSizePoints : 11d;

    private static double NormalizeLineSpacing(double lineSpacingMultiple) =>
        lineSpacingMultiple > 0d ? lineSpacingMultiple : 1d;
}

internal sealed record MeasuredTextRun(
    string Text,
    bool Bold,
    bool Italic,
    bool Underline,
    double FontSizePoints,
    string? FontFamilyName);

internal sealed record MeasuredTextStyle(
    bool Bold,
    bool Italic,
    bool Underline,
    double FontSizePoints,
    string? FontFamilyName);

internal sealed record MeasuredCharacter(char Value, MeasuredTextStyle Style);

internal sealed class MeasuredLine
{
    public MeasuredLine(
        IReadOnlyList<MeasuredCharacter> characters,
        double heightMillimeters,
        bool endsWithExplicitNewline)
    {
        Characters = characters;
        HeightMillimeters = heightMillimeters;
        EndsWithExplicitNewline = endsWithExplicitNewline;
    }

    public IReadOnlyList<MeasuredCharacter> Characters { get; }
    public double HeightMillimeters { get; }
    public bool EndsWithExplicitNewline { get; }
}

internal sealed class TextMeasurement
{
    public TextMeasurement(IReadOnlyList<MeasuredLine> lines) => Lines = lines;

    public IReadOnlyList<MeasuredLine> Lines { get; }
    public double TotalHeightMillimeters => Lines.Sum(line => line.HeightMillimeters);
    public double FirstLineHeightMillimeters => Lines.Count == 0 ? 0d : Lines[0].HeightMillimeters;
}
