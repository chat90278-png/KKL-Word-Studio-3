namespace KKL.WordStudio.Infrastructure.ReferenceFormatting;

using System.Globalization;
using System.Xml.Linq;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;

internal sealed class OpenXmlStyleResolver
{
    private const int MaxStyleDepth = 32;
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly Dictionary<string, StyleRecord> _styles;
    private readonly XElement? _defaultParagraphProperties;
    private readonly XElement? _defaultRunProperties;

    public OpenXmlStyleResolver(XDocument? stylesDocument)
    {
        var root = stylesDocument?.Root;
        _styles = root?
            .Elements(W + "style")
            .Select(style => new StyleRecord(
                GetAttribute(style, "styleId") ?? string.Empty,
                GetAttribute(style, "type") ?? string.Empty,
                GetAttribute(style.Element(W + "name"), "val") ?? string.Empty,
                GetAttribute(style.Element(W + "basedOn"), "val"),
                style.Element(W + "pPr"),
                style.Element(W + "rPr")))
            .Where(style => !string.IsNullOrWhiteSpace(style.Id))
            .GroupBy(style => style.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, StyleRecord>(StringComparer.Ordinal);

        _defaultParagraphProperties = root?
            .Element(W + "docDefaults")?
            .Element(W + "pPrDefault")?
            .Element(W + "pPr");
        _defaultRunProperties = root?
            .Element(W + "docDefaults")?
            .Element(W + "rPrDefault")?
            .Element(W + "rPr");
    }

    public ResolvedTextFormat ResolveParagraph(XElement paragraph)
    {
        var state = CreateDefaultState();
        ApplyRunProperties(state, _defaultRunProperties);
        ApplyParagraphProperties(state, _defaultParagraphProperties);

        var paragraphProperties = paragraph.Element(W + "pPr");
        var styleId = GetAttribute(paragraphProperties?.Element(W + "pStyle"), "val");
        foreach (var style in GetStyleChain(styleId))
        {
            ApplyRunProperties(state, style.RunProperties);
            ApplyParagraphProperties(state, style.ParagraphProperties);
        }

        ApplyRunProperties(state, paragraphProperties?.Element(W + "rPr"));
        ApplyParagraphProperties(state, paragraphProperties);
        return state.ToResolved();
    }

    public ResolvedTextFormat ResolveRun(XElement paragraph, XElement? run)
    {
        var state = CreateDefaultState();
        ApplyRunProperties(state, _defaultRunProperties);
        ApplyParagraphProperties(state, _defaultParagraphProperties);

        var paragraphProperties = paragraph.Element(W + "pPr");
        var paragraphStyleId = GetAttribute(paragraphProperties?.Element(W + "pStyle"), "val");
        foreach (var style in GetStyleChain(paragraphStyleId))
        {
            ApplyRunProperties(state, style.RunProperties);
            ApplyParagraphProperties(state, style.ParagraphProperties);
        }

        ApplyRunProperties(state, paragraphProperties?.Element(W + "rPr"));
        ApplyParagraphProperties(state, paragraphProperties);

        var runProperties = run?.Element(W + "rPr");
        var runStyleId = GetAttribute(runProperties?.Element(W + "rStyle"), "val");
        foreach (var style in GetStyleChain(runStyleId))
            ApplyRunProperties(state, style.RunProperties);

        ApplyRunProperties(state, runProperties);
        return state.ToResolved();
    }

    public ResolvedTextFormat ResolveNamedParagraphStyle(params string[] preferredNames)
    {
        var style = preferredNames
            .Select(NormalizeName)
            .SelectMany(name => _styles.Values.Where(candidate =>
                NormalizeName(candidate.Id) == name || NormalizeName(candidate.Name) == name))
            .FirstOrDefault();

        if (style is null)
            return CreateDefaultState().ToResolved();

        var paragraph = new XElement(W + "p",
            new XElement(W + "pPr",
                new XElement(W + "pStyle", new XAttribute(W + "val", style.Id))));
        return ResolveParagraph(paragraph);
    }

    public string? GetParagraphStyleId(XElement paragraph) =>
        GetAttribute(paragraph.Element(W + "pPr")?.Element(W + "pStyle"), "val");

    public string? GetStyleName(string? styleId) =>
        styleId is not null && _styles.TryGetValue(styleId, out var style) ? style.Name : null;

    public bool IsHeadingLike(XElement paragraph, ResolvedTextFormat bodyFormat)
    {
        var styleId = GetParagraphStyleId(paragraph);
        var styleName = GetStyleName(styleId);
        var normalizedId = NormalizeName(styleId);
        var normalizedName = NormalizeName(styleName);

        if (normalizedId.Contains("heading", StringComparison.Ordinal)
            || normalizedName.Contains("heading", StringComparison.Ordinal)
            || normalizedId.Contains("baslik", StringComparison.Ordinal)
            || normalizedName.Contains("baslik", StringComparison.Ordinal)
            || normalizedId.StartsWith("balk", StringComparison.Ordinal)
            || normalizedName.StartsWith("balk", StringComparison.Ordinal)
            || paragraph.Element(W + "pPr")?.Element(W + "outlineLvl") is not null)
        {
            return true;
        }

        var format = ResolveParagraph(paragraph);
        return format.KeepWithNext
            && (format.Bold || format.Italic || format.FontSizePoints >= bodyFormat.FontSizePoints + 1d);
    }

    public bool IsCaptionLike(XElement paragraph)
    {
        var styleId = NormalizeName(GetParagraphStyleId(paragraph));
        var styleName = NormalizeName(GetStyleName(GetParagraphStyleId(paragraph)));
        return styleId.Contains("caption", StringComparison.Ordinal)
            || styleName.Contains("caption", StringComparison.Ordinal)
            || styleId.Contains("resimyaz", StringComparison.Ordinal)
            || styleName.Contains("resimyaz", StringComparison.Ordinal);
    }

    private IEnumerable<StyleRecord> GetStyleChain(string? styleId)
    {
        if (string.IsNullOrWhiteSpace(styleId))
            return Array.Empty<StyleRecord>();

        var reversed = new List<StyleRecord>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentId = styleId;

        for (var depth = 0; depth < MaxStyleDepth && !string.IsNullOrWhiteSpace(currentId); depth++)
        {
            if (!visited.Add(currentId) || !_styles.TryGetValue(currentId, out var style))
                break;

            reversed.Add(style);
            currentId = style.BasedOnId;
        }

        reversed.Reverse();
        return reversed;
    }

    private static MutableFormatState CreateDefaultState() => new()
    {
        FontFamilyName = "Calibri",
        FontSizePoints = 11d,
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

    private static void ApplyRunProperties(MutableFormatState state, XElement? runProperties)
    {
        if (runProperties is null)
            return;

        var fonts = runProperties.Element(W + "rFonts");
        var fontFamily = GetAttribute(fonts, "ascii")
            ?? GetAttribute(fonts, "hAnsi")
            ?? GetAttribute(fonts, "eastAsia")
            ?? GetAttribute(fonts, "cs");
        if (!string.IsNullOrWhiteSpace(fontFamily))
            state.FontFamilyName = fontFamily;

        var fontSize = ParseDouble(GetAttribute(runProperties.Element(W + "sz"), "val"));
        if (fontSize is not null)
            state.FontSizePoints = fontSize.Value / 2d;

        var bold = ReadOnOff(runProperties.Element(W + "b"));
        if (bold is not null)
            state.Bold = bold.Value;

        var italic = ReadOnOff(runProperties.Element(W + "i"));
        if (italic is not null)
            state.Italic = italic.Value;

        var underline = runProperties.Element(W + "u");
        if (underline is not null)
        {
            var value = GetAttribute(underline, "val");
            state.Underline = !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }

        var color = GetAttribute(runProperties.Element(W + "color"), "val");
        if (!string.IsNullOrWhiteSpace(color) && !string.Equals(color, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = color.Trim().TrimStart('#').ToUpperInvariant();
            if (normalized.Length == 6 && normalized.All(Uri.IsHexDigit))
                state.ForegroundColor = $"#FF{normalized}";
            else if (normalized.Length == 8 && normalized.All(Uri.IsHexDigit))
                state.ForegroundColor = $"#{normalized}";
        }
    }

    private static void ApplyParagraphProperties(MutableFormatState state, XElement? paragraphProperties)
    {
        if (paragraphProperties is null)
            return;

        var justification = GetAttribute(paragraphProperties.Element(W + "jc"), "val");
        if (!string.IsNullOrWhiteSpace(justification))
        {
            state.Alignment = justification.ToLowerInvariant() switch
            {
                "center" => ParagraphAlignment.Center,
                "right" or "end" => ParagraphAlignment.Right,
                "both" or "distribute" or "justify" => ParagraphAlignment.Justify,
                _ => ParagraphAlignment.Left
            };
        }

        var spacing = paragraphProperties.Element(W + "spacing");
        var before = ParseDouble(GetAttribute(spacing, "before"));
        if (before is not null)
            state.SpaceBeforePoints = before.Value / 20d;
        var after = ParseDouble(GetAttribute(spacing, "after"));
        if (after is not null)
            state.SpaceAfterPoints = after.Value / 20d;

        var line = ParseDouble(GetAttribute(spacing, "line"));
        var lineRule = GetAttribute(spacing, "lineRule");
        if (line is not null && (string.IsNullOrWhiteSpace(lineRule)
            || string.Equals(lineRule, "auto", StringComparison.OrdinalIgnoreCase)))
        {
            state.LineSpacingMultiple = Math.Max(0.1d, line.Value / 240d);
        }

        var indentation = paragraphProperties.Element(W + "ind");
        var left = ParseDouble(GetAttribute(indentation, "left") ?? GetAttribute(indentation, "start"));
        if (left is not null)
            state.LeftIndentMillimeters = TwipsToMillimeters(left.Value);

        var firstLine = ParseDouble(GetAttribute(indentation, "firstLine"));
        if (firstLine is not null)
            state.FirstLineIndentMillimeters = TwipsToMillimeters(firstLine.Value);
        else
        {
            var hanging = ParseDouble(GetAttribute(indentation, "hanging"));
            if (hanging is not null)
                state.FirstLineIndentMillimeters = -TwipsToMillimeters(hanging.Value);
        }

        var keepNext = ReadOnOff(paragraphProperties.Element(W + "keepNext"));
        if (keepNext is not null)
            state.KeepWithNext = keepNext.Value;
    }

    private static bool? ReadOnOff(XElement? element)
    {
        if (element is null)
            return null;

        var value = GetAttribute(element, "val");
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.ToLowerInvariant() switch
        {
            "0" or "false" or "off" or "no" => false,
            _ => true
        };
    }

    internal static string? GetAttribute(XElement? element, string localName) =>
        element?.Attribute(W + localName)?.Value;

    internal static double? ParseDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return double.TryParse(text.Trim().TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    internal static double TwipsToMillimeters(double twips) => twips * 25.4d / 1440d;

    internal static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }

    private sealed record StyleRecord(
        string Id,
        string Type,
        string Name,
        string? BasedOnId,
        XElement? ParagraphProperties,
        XElement? RunProperties);

    private sealed class MutableFormatState
    {
        public required string FontFamilyName { get; set; }
        public required double FontSizePoints { get; set; }
        public required bool Bold { get; set; }
        public required bool Italic { get; set; }
        public required bool Underline { get; set; }
        public required string ForegroundColor { get; set; }
        public required ParagraphAlignment Alignment { get; set; }
        public required double SpaceBeforePoints { get; set; }
        public required double SpaceAfterPoints { get; set; }
        public required double LineSpacingMultiple { get; set; }
        public required double LeftIndentMillimeters { get; set; }
        public required double FirstLineIndentMillimeters { get; set; }
        public required bool KeepWithNext { get; set; }

        public ResolvedTextFormat ToResolved() => new()
        {
            FontFamilyName = FontFamilyName,
            FontSizePoints = FontSizePoints,
            Bold = Bold,
            Italic = Italic,
            Underline = Underline,
            ForegroundColor = ForegroundColor,
            Alignment = Alignment,
            SpaceBeforePoints = SpaceBeforePoints,
            SpaceAfterPoints = SpaceAfterPoints,
            LineSpacingMultiple = LineSpacingMultiple,
            LeftIndentMillimeters = LeftIndentMillimeters,
            FirstLineIndentMillimeters = FirstLineIndentMillimeters,
            KeepWithNext = KeepWithNext
        };
    }
}
