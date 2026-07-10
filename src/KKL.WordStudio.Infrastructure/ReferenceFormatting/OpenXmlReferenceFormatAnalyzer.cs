namespace KKL.WordStudio.Infrastructure.ReferenceFormatting;

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;

internal sealed class OpenXmlReferenceFormatAnalyzer
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly Regex SequenceInstructionRegex = new(
        @"\bSEQ\s+(?:""(?<id>[^""]+)""|(?<id>[^\s\\]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingSeparatorRegex = new(
        @"^[\s\p{P}\p{S}]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public DocumentFormatProfile Analyze(
        WordprocessingDocument document,
        CancellationToken cancellationToken = default)
    {
        var mainPart = document.MainDocumentPart
            ?? throw new InvalidDataException("DOCX ana belge bölümü bulunamadı.");
        if (mainPart.Document?.Body is null)
            throw new InvalidDataException("DOCX ana belge gövdesi bulunamadı.");

        cancellationToken.ThrowIfCancellationRequested();
        var documentXml = LoadXml(mainPart);
        var body = documentXml.Root?.Element(W + "body")
            ?? throw new InvalidDataException("WordprocessingML body öğesi bulunamadı.");
        var stylesXml = mainPart.StyleDefinitionsPart is null
            ? null
            : LoadXml(mainPart.StyleDefinitionsPart);
        var resolver = new OpenXmlStyleResolver(stylesXml);
        var warnings = new List<string>();

        var page = ExtractPageProfile(body, warnings);
        var bodyText = resolver.ResolveNamedParagraphStyle(
            "Normal",
            "Body Text",
            "BodyText",
            "Gövde Metni",
            "GvdeMetni");

        var bodyElements = body.Elements()
            .Where(element => element.Name == W + "p" || element.Name == W + "tbl")
            .ToList();
        var firstTableIndex = bodyElements.FindIndex(element => element.Name == W + "tbl");
        var preTableParagraphs = (firstTableIndex < 0 ? bodyElements : bodyElements.Take(firstTableIndex))
            .Where(element => element.Name == W + "p")
            .Where(paragraph => !string.IsNullOrWhiteSpace(GetVisibleText(paragraph)))
            .ToList();

        var headingParagraphs = preTableParagraphs
            .Where(paragraph => resolver.IsHeadingLike(paragraph, bodyText))
            .ToList();

        var primaryParagraph = headingParagraphs.FirstOrDefault();
        var primaryStyleId = primaryParagraph is null ? null : resolver.GetParagraphStyleId(primaryParagraph);
        var secondaryParagraph = headingParagraphs
            .Skip(primaryParagraph is null ? 0 : 1)
            .FirstOrDefault(paragraph =>
                !string.Equals(resolver.GetParagraphStyleId(paragraph), primaryStyleId, StringComparison.Ordinal)
                || !FormatsEquivalent(
                    ResolveEffectiveParagraphFormat(primaryParagraph!, resolver),
                    ResolveEffectiveParagraphFormat(paragraph, resolver)));

        if (primaryParagraph is null)
            warnings.Add("Referans belgede desteklenen birincil başlık örneği bulunamadı; gövde biçimi kullanıldı.");
        if (secondaryParagraph is null)
            warnings.Add("Referans belgede desteklenen ikincil başlık örneği bulunamadı; birincil başlık biçimi kullanıldı.");

        var primaryHeading = primaryParagraph is null
            ? bodyText
            : ResolveEffectiveParagraphFormat(primaryParagraph, resolver);
        var secondaryHeading = secondaryParagraph is null
            ? primaryHeading
            : ResolveEffectiveParagraphFormat(secondaryParagraph, resolver);

        var captionObservations = CollectCaptionObservations(bodyElements, resolver, cancellationToken);
        var tableCaption = captionObservations.FirstOrDefault()?.Format ?? bodyText;
        var captionSequence = captionObservations
            .Select(observation => observation.Sequence)
            .FirstOrDefault(sequence => sequence is not null);

        AppendCaptionWarnings(captionObservations, warnings);
        AppendSecondaryHeadingWarnings(bodyElements, resolver, secondaryParagraph, warnings);

        var tables = bodyElements.Where(element => element.Name == W + "tbl").ToList();
        var tableProfiles = new List<ReferenceTableFormatProfile>(tables.Count);
        for (var index = 0; index < tables.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            tableProfiles.Add(ExtractTableProfile(tables[index], index, resolver, warnings));
        }

        if (tableProfiles.Count == 0)
            warnings.Add("Referans belgede desteklenen tablo biçimi bulunamadı.");

        return new DocumentFormatProfile
        {
            Page = page,
            PrimaryHeading = primaryHeading,
            SecondaryHeading = secondaryHeading,
            BodyText = bodyText,
            TableCaption = tableCaption,
            TableCaptionSequence = captionSequence,
            TableFormats = tableProfiles,
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static PageFormatProfile ExtractPageProfile(XElement body, List<string> warnings)
    {
        var sectionProperties = body.Elements(W + "sectPr").LastOrDefault()
            ?? body.Descendants(W + "sectPr").LastOrDefault();
        var pageSize = sectionProperties?.Element(W + "pgSz");
        var pageMargin = sectionProperties?.Element(W + "pgMar");

        var widthTwips = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(pageSize, "w"));
        var heightTwips = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(pageSize, "h"));
        var topTwips = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(pageMargin, "top"));
        var bottomTwips = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(pageMargin, "bottom"));
        var leftTwips = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(pageMargin, "left"));
        var rightTwips = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(pageMargin, "right"));
        var headerTwips = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(pageMargin, "header"));
        var footerTwips = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(pageMargin, "footer"));

        if (widthTwips is null || heightTwips is null)
            warnings.Add("Referans belge sayfa boyutu tam çözülemedi; A4 uyumlu varsayılan geometri kullanıldı.");
        if (topTwips is null || bottomTwips is null || leftTwips is null || rightTwips is null)
            warnings.Add("Referans belge kenar boşlukları tam çözülemedi; desteklenen varsayılanlar kullanıldı.");

        return new PageFormatProfile
        {
            WidthMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(widthTwips ?? 11906d),
            HeightMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(heightTwips ?? 16838d),
            MarginTopMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(topTwips ?? 1417d),
            MarginBottomMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(bottomTwips ?? 1417d),
            MarginLeftMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(leftTwips ?? 1417d),
            MarginRightMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(rightTwips ?? 1417d),
            HeaderDistanceMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(headerTwips ?? 720d),
            FooterDistanceMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(footerTwips ?? 720d)
        };
    }

    private static List<CaptionObservation> CollectCaptionObservations(
        IReadOnlyList<XElement> bodyElements,
        OpenXmlStyleResolver resolver,
        CancellationToken cancellationToken)
    {
        var observations = new List<CaptionObservation>();
        for (var index = 0; index < bodyElements.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (bodyElements[index].Name != W + "tbl" || index == 0)
                continue;

            var paragraph = bodyElements[index - 1];
            if (paragraph.Name != W + "p")
                continue;

            var sequence = TryExtractSequence(paragraph);
            if (!resolver.IsCaptionLike(paragraph) && sequence is null)
                continue;

            observations.Add(new CaptionObservation(
                paragraph,
                ResolveEffectiveParagraphFormat(paragraph, resolver),
                sequence));
        }

        return observations;
    }

    private static void AppendCaptionWarnings(
        IReadOnlyList<CaptionObservation> observations,
        List<string> warnings)
    {
        var sequences = observations
            .Where(observation => observation.Sequence is not null)
            .Select(observation => observation.Sequence!)
            .ToList();

        foreach (var group in sequences.GroupBy(sequence => sequence.SequenceIdentifier, StringComparer.OrdinalIgnoreCase))
        {
            var labels = group.Select(sequence => sequence.DisplayLabel)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (labels.Count > 1)
            {
                warnings.Add(
                    $"SEQ '{group.Key}' için birden fazla görünür etiket bulundu ({string.Join(", ", labels)}); ilk etiket seçildi.");
            }
        }

        var captionSizes = observations
            .Select(observation => Math.Round(observation.Format.FontSizePoints, 4))
            .Distinct()
            .ToList();
        if (captionSizes.Count > 1)
        {
            warnings.Add(
                "Referans tablo açıklamalarında farklı etkin yazı boyutları bulundu; ilk açıklama biçimi seçildi.");
        }
    }

    private static void AppendSecondaryHeadingWarnings(
        IReadOnlyList<XElement> bodyElements,
        OpenXmlStyleResolver resolver,
        XElement? secondaryParagraph,
        List<string> warnings)
    {
        if (secondaryParagraph is null)
            return;

        var styleId = resolver.GetParagraphStyleId(secondaryParagraph);
        if (string.IsNullOrWhiteSpace(styleId))
            return;

        var indents = bodyElements
            .Where(element => element.Name == W + "p")
            .Where(paragraph => string.Equals(resolver.GetParagraphStyleId(paragraph), styleId, StringComparison.Ordinal))
            .Where(paragraph => !string.IsNullOrWhiteSpace(GetVisibleText(paragraph)))
            .Select(paragraph => Math.Round(ResolveEffectiveParagraphFormat(paragraph, resolver).LeftIndentMillimeters, 4))
            .Distinct()
            .ToList();

        if (indents.Count > 1)
        {
            warnings.Add(
                "İkincil başlık örneklerinde farklı sol girintiler bulundu; ilk ikincil başlık örneği referans alındı.");
        }
    }

    private static ReferenceTableFormatProfile ExtractTableProfile(
        XElement table,
        int tableIndex,
        OpenXmlStyleResolver resolver,
        List<string> warnings)
    {
        var rows = table.Elements(W + "tr").ToList();
        var headerCells = rows.FirstOrDefault()?.Elements(W + "tc").ToList() ?? new List<XElement>();
        var gridWidths = table.Element(W + "tblGrid")?
            .Elements(W + "gridCol")
            .Select(column => OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(column, "w")) ?? 0d)
            .ToList()
            ?? new List<double>();

        var columnCount = Math.Max(headerCells.Count, gridWidths.Count);
        if (columnCount == 0)
            columnCount = rows.Select(row => row.Elements(W + "tc").Count()).DefaultIfEmpty(0).Max();

        var headers = Enumerable.Range(0, columnCount)
            .Select(index => index < headerCells.Count ? NormalizeVisibleCellText(headerCells[index]) : string.Empty)
            .ToArray();

        var normalizedWeights = NormalizeWeights(gridWidths, columnCount);
        var columns = new List<ResolvedTableColumnFormat>(columnCount);
        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var headerCell = columnIndex < headerCells.Count ? headerCells[columnIndex] : null;
            var bodyCells = rows.Skip(1)
                .Select(row => row.Elements(W + "tc").ElementAtOrDefault(columnIndex))
                .Where(cell => cell is not null)
                .Cast<XElement>()
                .ToList();

            var headerSample = headerCell is null
                ? CellFormatSample.FromFormat(DefaultCellFormat(), VerticalContentAlignment.Top, false)
                : ResolveCellSample(headerCell, resolver);
            var bodySample = ResolveDominantCellSample(bodyCells, resolver) ?? headerSample;

            AppendMixedBodyFormatWarning(bodyCells, resolver, tableIndex, headers.ElementAtOrDefault(columnIndex), warnings);

            columns.Add(new ResolvedTableColumnFormat
            {
                WidthWeight = normalizedWeights[columnIndex],
                HeaderAlignment = headerSample.Alignment,
                BodyAlignment = bodySample.Alignment,
                HeaderFontFamilyName = headerSample.FontFamilyName,
                HeaderFontSizePoints = headerSample.FontSizePoints,
                HeaderBold = headerSample.Bold,
                BodyFontFamilyName = bodySample.FontFamilyName,
                BodyFontSizePoints = bodySample.FontSizePoints,
                BodyBold = bodySample.Bold,
                VerticalAlignment = ResolveDominantVerticalAlignment(bodyCells, headerCell),
                NoWrap = ResolveDominantNoWrap(bodyCells)
            });
        }

        var tableProperties = table.Element(W + "tblPr");
        var tableWidth = tableProperties?.Element(W + "tblW");
        var widthType = OpenXmlStyleResolver.GetAttribute(tableWidth, "type");
        var widthValue = OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(tableWidth, "w"));
        var widthPercent = string.Equals(widthType, "pct", StringComparison.OrdinalIgnoreCase) && widthValue is not null
            ? widthValue.Value / 50d
            : 100d;

        var layoutType = OpenXmlStyleResolver.GetAttribute(tableProperties?.Element(W + "tblLayout"), "type");
        var borderSizes = tableProperties?.Element(W + "tblBorders")?
            .Elements()
            .Select(border => OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(border, "sz")))
            .Where(size => size is not null)
            .Select(size => size!.Value / 8d)
            .ToList()
            ?? new List<double>();
        var borderSize = DominantDouble(borderSizes) ?? 0d;

        var cellMargins = tableProperties?.Element(W + "tblCellMar");
        var marginTop = ReadTwipsChild(cellMargins, "top");
        var marginBottom = ReadTwipsChild(cellMargins, "bottom");
        var marginLeft = ReadTwipsChild(cellMargins, "left") ?? ReadTwipsChild(cellMargins, "start");
        var marginRight = ReadTwipsChild(cellMargins, "right") ?? ReadTwipsChild(cellMargins, "end");

        var rowHeights = rows
            .Select(row => row.Element(W + "trPr")?.Element(W + "trHeight"))
            .Select(height => OpenXmlStyleResolver.ParseDouble(OpenXmlStyleResolver.GetAttribute(height, "val")))
            .Where(height => height is > 0d)
            .Select(height => OpenXmlStyleResolver.TwipsToMillimeters(height!.Value))
            .ToList();

        var repeatHeader = ReadOnOff(rows.FirstOrDefault()?.Element(W + "trPr")?.Element(W + "tblHeader")) ?? false;

        return new ReferenceTableFormatProfile
        {
            Key = $"table-{tableIndex + 1:000}",
            DisplayName = $"Referans Tablo {tableIndex + 1}",
            ReferenceHeaders = headers,
            Format = new ResolvedTableFormat
            {
                WidthPercent = widthPercent,
                FixedLayout = string.Equals(layoutType, "fixed", StringComparison.OrdinalIgnoreCase),
                BorderSizePoints = borderSize,
                CellMarginTopMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(marginTop ?? 0d),
                CellMarginBottomMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(marginBottom ?? 0d),
                CellMarginLeftMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(marginLeft ?? 0d),
                CellMarginRightMillimeters = OpenXmlStyleResolver.TwipsToMillimeters(marginRight ?? 0d),
                PreferredRowHeightMillimeters = DominantDouble(rowHeights) ?? 0d,
                RepeatHeader = repeatHeader,
                Columns = columns
            }
        };
    }

    private static void AppendMixedBodyFormatWarning(
        IReadOnlyList<XElement> bodyCells,
        OpenXmlStyleResolver resolver,
        int tableIndex,
        string? header,
        List<string> warnings)
    {
        var formats = bodyCells
            .SelectMany(cell => ResolveRunFormats(cell, resolver))
            .Select(format => new FontFormatKey(
                format.FontFamilyName,
                Math.Round(format.FontSizePoints, 4),
                format.Bold,
                format.Italic,
                format.Underline))
            .Distinct()
            .ToList();

        if (formats.Count > 1)
        {
            var columnName = string.IsNullOrWhiteSpace(header) ? "adsız sütun" : header;
            warnings.Add(
                $"Referans Tablo {tableIndex + 1} / '{columnName}' gövde hücrelerinde karışık yazı biçimleri bulundu; baskın biçim seçildi.");
        }
    }

    private static IReadOnlyList<double> NormalizeWeights(IReadOnlyList<double> gridWidths, int columnCount)
    {
        if (columnCount <= 0)
            return Array.Empty<double>();

        var widths = Enumerable.Range(0, columnCount)
            .Select(index => index < gridWidths.Count && gridWidths[index] > 0d ? gridWidths[index] : 0d)
            .ToArray();
        var sum = widths.Sum();
        if (sum <= 0d)
            return Enumerable.Repeat(100d / columnCount, columnCount).ToArray();

        return widths.Select(width => width / sum * 100d).ToArray();
    }

    private static CellFormatSample? ResolveDominantCellSample(
        IReadOnlyList<XElement> cells,
        OpenXmlStyleResolver resolver)
    {
        var samples = cells
            .Where(cell => !string.IsNullOrWhiteSpace(GetVisibleText(cell)))
            .Select(cell => ResolveCellSample(cell, resolver))
            .ToList();
        if (samples.Count == 0)
            return null;

        return Dominant(samples, sample => new CellFormatKey(
            sample.Alignment,
            sample.FontFamilyName,
            Math.Round(sample.FontSizePoints, 4),
            sample.Bold));
    }

    private static CellFormatSample ResolveCellSample(XElement cell, OpenXmlStyleResolver resolver)
    {
        var paragraphs = cell.Descendants(W + "p").ToList();
        var paragraphFormats = paragraphs
            .Where(paragraph => !string.IsNullOrWhiteSpace(GetVisibleText(paragraph)))
            .Select(paragraph => ResolveEffectiveParagraphFormat(paragraph, resolver))
            .ToList();
        var paragraphFormat = paragraphFormats.Count == 0
            ? DefaultCellFormat()
            : Dominant(paragraphFormats, format => new ParagraphFormatKey(
                format.Alignment,
                format.FontFamilyName,
                Math.Round(format.FontSizePoints, 4),
                format.Bold));

        var runFormats = ResolveRunFormats(cell, resolver);
        var runFormat = runFormats.Count == 0
            ? paragraphFormat
            : Dominant(runFormats, format => new FontFormatKey(
                format.FontFamilyName,
                Math.Round(format.FontSizePoints, 4),
                format.Bold,
                format.Italic,
                format.Underline));

        return new CellFormatSample(
            paragraphFormat.Alignment,
            runFormat.FontFamilyName,
            runFormat.FontSizePoints,
            runFormat.Bold,
            ReadVerticalAlignment(cell) ?? VerticalContentAlignment.Top,
            ReadOnOff(cell.Element(W + "tcPr")?.Element(W + "noWrap")) ?? false);
    }

    private static List<ResolvedTextFormat> ResolveRunFormats(XElement cell, OpenXmlStyleResolver resolver)
    {
        var formats = new List<ResolvedTextFormat>();
        foreach (var run in cell.Descendants(W + "r"))
        {
            var text = string.Concat(run.Descendants(W + "t").Select(element => element.Value));
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var paragraph = run.Ancestors(W + "p").FirstOrDefault();
            if (paragraph is not null)
                formats.Add(resolver.ResolveRun(paragraph, run));
        }

        return formats;
    }

    private static VerticalContentAlignment ResolveDominantVerticalAlignment(
        IReadOnlyList<XElement> bodyCells,
        XElement? headerCell)
    {
        var values = bodyCells
            .Select(ReadVerticalAlignment)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToList();
        if (values.Count == 0 && headerCell is not null && ReadVerticalAlignment(headerCell) is { } headerValue)
            values.Add(headerValue);

        return values.Count == 0
            ? VerticalContentAlignment.Top
            : Dominant(values, value => value);
    }

    private static bool ResolveDominantNoWrap(IReadOnlyList<XElement> bodyCells)
    {
        if (bodyCells.Count == 0)
            return false;

        var values = bodyCells
            .Select(cell => ReadOnOff(cell.Element(W + "tcPr")?.Element(W + "noWrap")) ?? false)
            .ToList();
        return values.Count(value => value) * 2 >= values.Count;
    }

    private static VerticalContentAlignment? ReadVerticalAlignment(XElement cell)
    {
        var value = OpenXmlStyleResolver.GetAttribute(cell.Element(W + "tcPr")?.Element(W + "vAlign"), "val");
        return value?.ToLowerInvariant() switch
        {
            "center" => VerticalContentAlignment.Center,
            "bottom" => VerticalContentAlignment.Bottom,
            "top" => VerticalContentAlignment.Top,
            _ => null
        };
    }

    private static double? ReadTwipsChild(XElement? parent, string localName) =>
        OpenXmlStyleResolver.ParseDouble(
            OpenXmlStyleResolver.GetAttribute(parent?.Element(W + localName), "w"));

    private static double? DominantDouble(IReadOnlyList<double> values) =>
        values.Count == 0 ? null : Dominant(values, value => Math.Round(value, 4));

    private static T Dominant<T, TKey>(IReadOnlyList<T> values, Func<T, TKey> keySelector)
        where TKey : notnull
    {
        if (values.Count == 0)
            throw new ArgumentException("At least one value is required.", nameof(values));

        return values
            .Select((value, index) => new { value, index, key = keySelector(value) })
            .GroupBy(item => item.key)
            .Select(group => new
            {
                Value = group.First().value,
                Count = group.Count(),
                FirstIndex = group.Min(item => item.index)
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.FirstIndex)
            .First()
            .Value;
    }

    private static ResolvedTextFormat ResolveEffectiveParagraphFormat(
        XElement paragraph,
        OpenXmlStyleResolver resolver)
    {
        var paragraphFormat = resolver.ResolveParagraph(paragraph);
        var runFormats = paragraph.Descendants(W + "r")
            .Select(run => new
            {
                Run = run,
                TextLength = string.Concat(run.Descendants(W + "t").Select(text => text.Value)).Trim().Length
            })
            .Where(item => item.TextLength > 0)
            .Select(item => new
            {
                Format = resolver.ResolveRun(paragraph, item.Run),
                item.TextLength
            })
            .ToList();

        if (runFormats.Count == 0)
            return paragraphFormat;

        var dominant = runFormats
            .GroupBy(item => new FontFormatKey(
                item.Format.FontFamilyName,
                Math.Round(item.Format.FontSizePoints, 4),
                item.Format.Bold,
                item.Format.Italic,
                item.Format.Underline))
            .Select(group => new
            {
                Format = group.First().Format,
                Weight = group.Sum(item => item.TextLength),
                FirstIndex = runFormats.IndexOf(group.First())
            })
            .OrderByDescending(item => item.Weight)
            .ThenBy(item => item.FirstIndex)
            .First()
            .Format;

        return new ResolvedTextFormat
        {
            FontFamilyName = dominant.FontFamilyName,
            FontSizePoints = dominant.FontSizePoints,
            Bold = dominant.Bold,
            Italic = dominant.Italic,
            Underline = dominant.Underline,
            ForegroundColor = dominant.ForegroundColor,
            Alignment = paragraphFormat.Alignment,
            SpaceBeforePoints = paragraphFormat.SpaceBeforePoints,
            SpaceAfterPoints = paragraphFormat.SpaceAfterPoints,
            LineSpacingMultiple = paragraphFormat.LineSpacingMultiple,
            LeftIndentMillimeters = paragraphFormat.LeftIndentMillimeters,
            FirstLineIndentMillimeters = paragraphFormat.FirstLineIndentMillimeters,
            KeepWithNext = paragraphFormat.KeepWithNext
        };
    }

    private static bool FormatsEquivalent(ResolvedTextFormat left, ResolvedTextFormat right) =>
        string.Equals(left.FontFamilyName, right.FontFamilyName, StringComparison.OrdinalIgnoreCase)
        && Math.Abs(left.FontSizePoints - right.FontSizePoints) < 0.001d
        && left.Bold == right.Bold
        && left.Italic == right.Italic
        && left.Underline == right.Underline
        && left.Alignment == right.Alignment
        && Math.Abs(left.LeftIndentMillimeters - right.LeftIndentMillimeters) < 0.001d;

    private static TableCaptionSequenceProfile? TryExtractSequence(XElement paragraph)
    {
        var state = new SequenceScanState();
        VisitInline(paragraph, state);
        if (!state.SequenceFound || string.IsNullOrWhiteSpace(state.SequenceIdentifier))
            return null;

        var displayLabel = NormalizeVisibleText(state.Before.ToString()).Trim();
        var after = state.After.ToString();
        var separator = LeadingSeparatorRegex.Match(after).Value;

        return new TableCaptionSequenceProfile
        {
            DisplayLabel = displayLabel,
            SequenceIdentifier = state.SequenceIdentifier,
            Separator = separator
        };
    }

    private static void VisitInline(XElement element, SequenceScanState state)
    {
        if (element.Name == W + "fldSimple")
        {
            var instruction = OpenXmlStyleResolver.GetAttribute(element, "instr");
            if (!state.SequenceFound && TryParseSequenceIdentifier(instruction, out var identifier))
            {
                state.SequenceFound = true;
                state.SequenceIdentifier = identifier;
            }
            return; // cached field result text is intentionally ignored
        }

        if (element.Name == W + "fldChar")
        {
            var fieldType = OpenXmlStyleResolver.GetAttribute(element, "fldCharType")?.ToLowerInvariant();
            if (fieldType == "begin")
            {
                state.InComplexField = true;
                state.CurrentInstruction.Clear();
            }
            else if (fieldType == "end")
            {
                if (!state.SequenceFound
                    && TryParseSequenceIdentifier(state.CurrentInstruction.ToString(), out var identifier))
                {
                    state.SequenceFound = true;
                    state.SequenceIdentifier = identifier;
                }

                state.InComplexField = false;
                state.CurrentInstruction.Clear();
            }
            return;
        }

        if (element.Name == W + "instrText" && state.InComplexField)
        {
            state.CurrentInstruction.Append(element.Value);
            return;
        }

        if (element.Name == W + "t")
        {
            if (state.InComplexField)
                return;

            if (state.SequenceFound)
                state.After.Append(element.Value);
            else
                state.Before.Append(element.Value);
            return;
        }

        foreach (var child in element.Elements())
            VisitInline(child, state);
    }

    private static bool TryParseSequenceIdentifier(string? instruction, out string identifier)
    {
        identifier = string.Empty;
        if (string.IsNullOrWhiteSpace(instruction))
            return false;

        var match = SequenceInstructionRegex.Match(instruction);
        if (!match.Success)
            return false;

        identifier = match.Groups["id"].Value.Trim();
        return identifier.Length > 0;
    }

    private static string NormalizeVisibleCellText(XElement cell) =>
        NormalizeVisibleText(string.Join(" ", cell.Descendants(W + "p").Select(GetVisibleText)));

    private static string NormalizeVisibleText(string value) =>
        WhitespaceRegex.Replace(value, " ").Trim();

    private static string GetVisibleText(XElement element)
    {
        var builder = new StringBuilder();
        AppendVisibleText(element, builder);
        return builder.ToString();
    }

    private static void AppendVisibleText(XElement element, StringBuilder builder)
    {
        if (element.Name == W + "t" || element.Name == W + "instrText")
        {
            builder.Append(element.Value);
            return;
        }

        if (element.Name == W + "tab")
        {
            builder.Append('\t');
            return;
        }

        if (element.Name == W + "br" || element.Name == W + "cr")
        {
            builder.Append('\n');
            return;
        }

        foreach (var child in element.Elements())
            AppendVisibleText(child, builder);
    }

    private static bool? ReadOnOff(XElement? element)
    {
        if (element is null)
            return null;

        var value = OpenXmlStyleResolver.GetAttribute(element, "val");
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return value.ToLowerInvariant() switch
        {
            "0" or "false" or "off" or "no" => false,
            _ => true
        };
    }

    private static ResolvedTextFormat DefaultCellFormat() => new()
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

    private static XDocument LoadXml(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private sealed record CaptionObservation(
        XElement Paragraph,
        ResolvedTextFormat Format,
        TableCaptionSequenceProfile? Sequence);

    private sealed record CellFormatSample(
        ParagraphAlignment Alignment,
        string FontFamilyName,
        double FontSizePoints,
        bool Bold,
        VerticalContentAlignment VerticalAlignment,
        bool NoWrap)
    {
        public static CellFormatSample FromFormat(
            ResolvedTextFormat format,
            VerticalContentAlignment verticalAlignment,
            bool noWrap) => new(
                format.Alignment,
                format.FontFamilyName,
                format.FontSizePoints,
                format.Bold,
                verticalAlignment,
                noWrap);
    }

    private sealed class SequenceScanState
    {
        public bool SequenceFound { get; set; }
        public string SequenceIdentifier { get; set; } = string.Empty;
        public bool InComplexField { get; set; }
        public StringBuilder CurrentInstruction { get; } = new();
        public StringBuilder Before { get; } = new();
        public StringBuilder After { get; } = new();
    }

    private readonly record struct FontFormatKey(
        string FontFamilyName,
        double FontSizePoints,
        bool Bold,
        bool Italic,
        bool Underline);

    private readonly record struct ParagraphFormatKey(
        ParagraphAlignment Alignment,
        string FontFamilyName,
        double FontSizePoints,
        bool Bold);

    private readonly record struct CellFormatKey(
        ParagraphAlignment Alignment,
        string FontFamilyName,
        double FontSizePoints,
        bool Bold);
}
