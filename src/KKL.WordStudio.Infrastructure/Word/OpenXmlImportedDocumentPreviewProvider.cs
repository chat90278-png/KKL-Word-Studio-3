namespace KKL.WordStudio.Infrastructure.Word;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.ImportedDocuments;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Domain.Projects;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

/// <summary>
/// Reads a deliberately bounded, preview-oriented semantic projection from an
/// imported DOCX. The source package is always opened read-only. This is not a
/// Word rendering engine: unsupported constructs stay visible as warnings or
/// placeholders so the preview never pretends to be pixel-identical to Word.
/// </summary>
public sealed class OpenXmlImportedDocumentPreviewProvider : IImportedDocumentPreviewProvider
{
    private const double TwipsPerMillimeter = 1440.0 / 25.4;
    private const double EmusPerMillimeter = 36_000.0;

    private static readonly PageLayout DefaultPageLayout = new()
    {
        WidthMillimeters = 210,
        HeightMillimeters = 297,
        MarginTopMillimeters = 20,
        MarginBottomMillimeters = 20,
        MarginLeftMillimeters = 20,
        MarginRightMillimeters = 20,
        ShowPageNumbers = false
    };

    public Task<ImportedDocumentPreviewResult> ReadAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        if (project.FrontMatter is null)
        {
            return Task.FromResult(new ImportedDocumentPreviewResult
            {
                Document = null,
                IsMissing = false,
                StatusMessage = null
            });
        }

        var path = FrontMatterSourcePathResolver.Resolve(project.FrontMatter);
        if (path is null)
        {
            return Task.FromResult(new ImportedDocumentPreviewResult
            {
                Document = null,
                IsMissing = true,
                StatusMessage = $"Ön belge bulunamadı: {project.FrontMatter.FileName}"
            });
        }

        try
        {
            using var document = WordprocessingDocument.Open(path, false);
            var mainPart = document.MainDocumentPart;
            var body = mainPart?.Document?.Body;
            if (mainPart is null || body is null)
            {
                return Task.FromResult(new ImportedDocumentPreviewResult
                {
                    Document = null,
                    IsMissing = true,
                    StatusMessage = "Ön belge okunamıyor: ana Word belge içeriği bulunamadı."
                });
            }

            var warnings = new WarningCollector();
            var styles = new NarrowStyleResolver(mainPart);
            var sections = ExtractSections(mainPart, body, styles, warnings, cancellationToken);

            return Task.FromResult(new ImportedDocumentPreviewResult
            {
                Document = new ImportedDocumentPreviewDocument
                {
                    Sections = sections,
                    Warnings = warnings.Items
                },
                IsMissing = false,
                StatusMessage = null
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(new ImportedDocumentPreviewResult
            {
                Document = null,
                IsMissing = true,
                StatusMessage = "Ön belge okunamıyor veya artık kullanılamıyor."
            });
        }
    }

    private static IReadOnlyList<ImportedDocumentSection> ExtractSections(
        MainDocumentPart mainPart,
        Body body,
        NarrowStyleResolver styles,
        WarningCollector warnings,
        CancellationToken cancellationToken)
    {
        var sections = new List<ImportedDocumentSection>();
        var currentBlocks = new List<ImportedDocumentBlock>();

        foreach (var child in body.ChildElements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (child is SectionProperties)
                continue;

            ExtractBodyElement(mainPart, child, currentBlocks, styles, warnings);

            if (child is Paragraph paragraph)
            {
                var paragraphSection = paragraph.ParagraphProperties?.GetFirstChild<SectionProperties>();
                if (paragraphSection is not null)
                {
                    sections.Add(CreateSection(paragraphSection, currentBlocks, warnings));
                    currentBlocks = new List<ImportedDocumentBlock>();
                }
            }
        }

        var finalSectionProperties = body.Elements<SectionProperties>().LastOrDefault();
        sections.Add(CreateSection(finalSectionProperties, currentBlocks, warnings));
        return sections;
    }

    private static ImportedDocumentSection CreateSection(
        SectionProperties? sectionProperties,
        List<ImportedDocumentBlock> blocks,
        WarningCollector warnings) =>
        new()
        {
            PageLayout = ReadPageLayout(sectionProperties, warnings),
            Blocks = blocks.ToArray()
        };

    private static void ExtractBodyElement(
        MainDocumentPart mainPart,
        OpenXmlElement element,
        List<ImportedDocumentBlock> blocks,
        NarrowStyleResolver styles,
        WarningCollector warnings)
    {
        switch (element)
        {
            case Paragraph paragraph:
                ExtractParagraph(mainPart, paragraph, blocks, styles, warnings);
                return;
            case Table table:
                blocks.Add(ExtractTable(table, warnings));
                return;
            case SdtBlock contentControl:
                warnings.Add("İçerik denetimi (content control) düz akış olarak önizleniyor.");
                foreach (var nested in contentControl.Descendants<OpenXmlElement>()
                             .Where(candidate => candidate.Parent is SdtContentBlock && candidate is Paragraph or Table))
                {
                    ExtractBodyElement(mainPart, nested, blocks, styles, warnings);
                }
                return;
            case AltChunk:
                AddUnsupported(blocks, warnings, "İç içe altChunk içeriği önizleme sözleşmesi tarafından desteklenmiyor.");
                return;
            default:
                AddUnsupported(blocks, warnings, $"Word gövde öğesi desteklenmiyor: {element.LocalName}.");
                return;
        }
    }

    private static void ExtractParagraph(
        MainDocumentPart mainPart,
        Paragraph paragraph,
        List<ImportedDocumentBlock> blocks,
        NarrowStyleResolver styles,
        WarningCollector warnings)
    {
        var initialBlockCount = blocks.Count;
        var paragraphStyleId = styles.GetParagraphStyleId(paragraph);
        var alignment = styles.ResolveParagraphAlignment(paragraph, paragraphStyleId);
        var keepWithNext = styles.ResolveKeepWithNext(paragraph, paragraphStyleId);

        if (styles.ResolvePageBreakBefore(paragraph, paragraphStyleId))
            blocks.Add(new ImportedExplicitPageBreakBlock());

        if (paragraph.Descendants<FieldChar>().Any() || paragraph.Descendants<FieldCode>().Any())
            warnings.Add("Karmaşık Word alanı çalıştırılmadı; yalnızca belgede bulunan görünür sonuç metni önizleniyor.");

        if (paragraph.Descendants<InsertedRun>().Any() || paragraph.Descendants<DeletedRun>().Any())
            warnings.Add("İzlenen değişiklikler tam revizyon görünümüyle modellenmiyor; mevcut görünür metin en iyi çabayla çıkarılıyor.");
        if (paragraph.Descendants().Any(element => element.LocalName is "commentReference" or "commentRangeStart" or "commentRangeEnd"))
            warnings.Add("Word yorumları ve yorum aralıkları önizleme modeline dahil değil.");
        if (paragraph.ParagraphProperties?.GetFirstChild<NumberingProperties>() is not null)
            warnings.Add("Gelişmiş Word numaralandırma sadakati modellenmiyor; paragraf metni numara işareti olmadan önizlenebilir.");

        var pendingRuns = new List<ImportedTextRun>();
        foreach (var child in paragraph.ChildElements)
        {
            if (child is ParagraphProperties)
                continue;

            ProcessParagraphElement(mainPart, child, paragraphStyleId, pendingRuns, blocks, alignment, keepWithNext, styles, warnings);
        }

        FlushParagraph(pendingRuns, blocks, alignment, keepWithNext, preserveEmptyParagraph: blocks.Count == initialBlockCount);
    }

    private static void ProcessParagraphElement(
        MainDocumentPart mainPart,
        OpenXmlElement element,
        string? paragraphStyleId,
        List<ImportedTextRun> pendingRuns,
        List<ImportedDocumentBlock> blocks,
        ParagraphAlignment alignment,
        bool keepWithNext,
        NarrowStyleResolver styles,
        WarningCollector warnings)
    {
        if (element is Run directRun)
        {
            ProcessRun(mainPart, directRun, paragraphStyleId, pendingRuns, blocks, alignment, keepWithNext, styles, warnings);
            return;
        }

        if (element is SimpleField simpleField)
        {
            warnings.Add("Word alanı çalıştırılmadı; saklanan görünür alan sonucu önizleniyor.");
            foreach (var run in simpleField.Descendants<Run>())
                ProcessRun(mainPart, run, paragraphStyleId, pendingRuns, blocks, alignment, keepWithNext, styles, warnings);
            return;
        }

        if (element is Hyperlink hyperlink)
        {
            foreach (var run in hyperlink.Descendants<Run>())
                ProcessRun(mainPart, run, paragraphStyleId, pendingRuns, blocks, alignment, keepWithNext, styles, warnings);
            return;
        }

        if (element is InsertedRun insertedRun)
        {
            foreach (var run in insertedRun.Descendants<Run>())
                ProcessRun(mainPart, run, paragraphStyleId, pendingRuns, blocks, alignment, keepWithNext, styles, warnings);
            return;
        }

        if (element is DeletedRun)
            return;

        if (element.LocalName is "bookmarkStart" or "bookmarkEnd" or "proofErr" or "commentRangeStart" or "commentRangeEnd")
            return;

        if (ContainsMeaningfulUnsupportedContent(element))
        {
            FlushParagraph(pendingRuns, blocks, alignment, keepWithNext);
            AddUnsupported(blocks, warnings, DescribeUnsupported(element));
            return;
        }

        var nestedRuns = element.Descendants<Run>().ToArray();
        if (nestedRuns.Length > 0)
        {
            warnings.Add($"{element.LocalName} satır içi Word kapsayıcısı akış içeriği olarak önizleniyor.");
            foreach (var nestedRun in nestedRuns)
                ProcessRun(mainPart, nestedRun, paragraphStyleId, pendingRuns, blocks, alignment, keepWithNext, styles, warnings);
            return;
        }

        if (!string.IsNullOrWhiteSpace(element.InnerText))
        {
            FlushParagraph(pendingRuns, blocks, alignment, keepWithNext);
            AddUnsupported(blocks, warnings, $"{element.LocalName} Word içeriği doğrudan modellenemedi.");
        }
    }

    private static void ProcessRun(
        MainDocumentPart mainPart,
        Run run,
        string? paragraphStyleId,
        List<ImportedTextRun> pendingRuns,
        List<ImportedDocumentBlock> blocks,
        ParagraphAlignment alignment,
        bool keepWithNext,
        NarrowStyleResolver styles,
        WarningCollector warnings)
    {
        var formatting = styles.ResolveRunFormatting(run, paragraphStyleId);
        var text = new System.Text.StringBuilder();

        void FlushText()
        {
            if (text.Length == 0)
                return;

            pendingRuns.Add(new ImportedTextRun
            {
                Text = text.ToString(),
                Bold = formatting.Bold,
                Italic = formatting.Italic,
                Underline = formatting.Underline,
                FontSizePoints = formatting.FontSizePoints,
                FontFamilyName = formatting.FontFamilyName
            });
            text.Clear();
        }

        foreach (var child in run.ChildElements)
        {
            switch (child)
            {
                case RunProperties:
                case FieldCode:
                case FieldChar:
                    break;
                case Text value:
                    text.Append(value.Text);
                    break;
                case TabChar:
                    text.Append('\t');
                    break;
                case CarriageReturn:
                    text.Append('\n');
                    break;
                case Break wordBreak when wordBreak.Type?.Value == BreakValues.Page:
                    FlushText();
                    FlushParagraph(pendingRuns, blocks, alignment, keepWithNext);
                    blocks.Add(new ImportedExplicitPageBreakBlock());
                    break;
                case Break:
                    text.Append('\n');
                    break;
                case Drawing drawing:
                    FlushText();
                    FlushParagraph(pendingRuns, blocks, alignment, keepWithNext);
                    ExtractDrawing(mainPart, drawing, blocks, warnings);
                    break;
                case Picture:
                    FlushText();
                    FlushParagraph(pendingRuns, blocks, alignment, keepWithNext);
                    AddUnsupported(blocks, warnings, "VML/legacy Word şekli veya resmi desteklenmiyor.");
                    break;
                case FootnoteReference:
                    warnings.Add("Dipnot gövde içeriği önizleme modeline dahil değil.");
                    text.Append("[Dipnot]");
                    break;
                case EndnoteReference:
                    warnings.Add("Sonnot gövde içeriği önizleme modeline dahil değil.");
                    text.Append("[Sonnot]");
                    break;
                default:
                    if (ContainsMeaningfulUnsupportedContent(child))
                    {
                        FlushText();
                        FlushParagraph(pendingRuns, blocks, alignment, keepWithNext);
                        AddUnsupported(blocks, warnings, DescribeUnsupported(child));
                    }
                    else
                    {
                        foreach (var nestedText in child.Descendants<Text>())
                            text.Append(nestedText.Text);
                    }
                    break;
            }
        }

        FlushText();
    }

    private static void ExtractDrawing(
        MainDocumentPart mainPart,
        Drawing drawing,
        List<ImportedDocumentBlock> blocks,
        WarningCollector warnings)
    {
        var blip = drawing.Descendants<A.Blip>().FirstOrDefault();
        var relationshipId = blip?.Embed?.Value;
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            AddUnsupported(blocks, warnings, DescribeUnsupported(drawing));
            return;
        }

        if (drawing.Descendants<DW.Anchor>().Any())
            warnings.Add("Yüzen Word resmi akış içi resim olarak önizleniyor; kesin konumlandırma korunmuyor.");

        ImagePart? imagePart;
        try
        {
            imagePart = mainPart.GetPartById(relationshipId) as ImagePart;
        }
        catch (Exception)
        {
            imagePart = null;
        }

        if (imagePart is null)
        {
            AddUnsupported(blocks, warnings, "Word çizimindeki ilişki bir resim parçasına çözümlenemedi.");
            return;
        }

        byte[] bytes;
        try
        {
            using var imageStream = imagePart.GetStream(FileMode.Open, FileAccess.Read);
            using var copy = new MemoryStream();
            imageStream.CopyTo(copy);
            bytes = copy.ToArray();
        }
        catch (Exception)
        {
            AddUnsupported(blocks, warnings, "Word resmi okunamadı; resim içeriği önizlemeye aktarılamadı.");
            return;
        }

        var extent = drawing.Descendants<DW.Extent>().FirstOrDefault();
        var name = drawing.Descendants<DW.DocProperties>().FirstOrDefault()?.Name?.Value;
        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileName(imagePart.Uri.OriginalString);

        blocks.Add(new ImportedImageBlock
        {
            Name = name ?? "Word resmi",
            ImageBytes = bytes,
            ContentType = imagePart.ContentType,
            WidthMillimeters = extent?.Cx?.Value is long cx ? cx / EmusPerMillimeter : null,
            HeightMillimeters = extent?.Cy?.Value is long cy ? cy / EmusPerMillimeter : null
        });
    }

    private static ImportedTableBlock ExtractTable(Table table, WarningCollector warnings)
    {
        if (table.Descendants<GridSpan>().Any() || table.Descendants<VerticalMerge>().Any())
            warnings.Add("Birleştirilmiş Word tablo hücreleri geometrik olarak modellenmiyor; okunabilir hücre metni çıkarılıyor.");
        if (table.Descendants<Table>().Any())
            warnings.Add("İç içe Word tabloları düzen sadakati olmadan hücre metnine indirgeniyor.");
        if (table.Descendants<Drawing>().Any() || table.Descendants<Picture>().Any())
            warnings.Add("Tablo hücresi içindeki resimler ayrı akış resmi olarak modellenmiyor; hücre metni korunuyor.");

        var rows = table.Elements<TableRow>()
            .Select(row => (IReadOnlyList<string>)row.Elements<TableCell>()
                .Select(ReadCellText)
                .ToArray())
            .ToArray();

        var firstRow = table.Elements<TableRow>().FirstOrDefault();
        var repeatFirstRow = firstRow?.TableRowProperties?.GetFirstChild<TableHeader>() is not null;

        return new ImportedTableBlock
        {
            Rows = rows,
            RepeatFirstRow = repeatFirstRow
        };
    }

    private static string ReadCellText(TableCell cell)
    {
        var parts = new List<string>();
        foreach (var child in cell.ChildElements)
        {
            switch (child)
            {
                case TableCellProperties:
                    break;
                case Paragraph paragraph:
                    parts.Add(ReadCellParagraphText(paragraph));
                    break;
                case Table nestedTable:
                    parts.Add(ReadNestedTableText(nestedTable));
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(child.InnerText))
                        parts.Add(child.InnerText);
                    break;
            }
        }

        return string.Join("\n", parts);
    }

    private static string ReadNestedTableText(Table table)
    {
        return string.Join(
            "\n",
            table.Elements<TableRow>().Select(
                row => string.Join(
                    "\t",
                    row.Elements<TableCell>().Select(ReadCellText))));
    }

    private static string ReadCellParagraphText(Paragraph paragraph)
    {
        var text = new System.Text.StringBuilder();
        foreach (var descendant in paragraph.Descendants())
        {
            switch (descendant)
            {
                case Text value:
                    text.Append(value.Text);
                    break;
                case TabChar:
                    text.Append('\t');
                    break;
                case Break:
                case CarriageReturn:
                    text.Append('\n');
                    break;
            }
        }
        return text.ToString();
    }

    private static void FlushParagraph(
        List<ImportedTextRun> pendingRuns,
        List<ImportedDocumentBlock> blocks,
        ParagraphAlignment alignment,
        bool keepWithNext,
        bool preserveEmptyParagraph = false)
    {
        if (pendingRuns.Count == 0 && !preserveEmptyParagraph)
            return;

        blocks.Add(new ImportedParagraphBlock
        {
            Runs = pendingRuns.ToArray(),
            Alignment = alignment,
            KeepWithNext = keepWithNext
        });
        pendingRuns.Clear();
    }

    private static PageLayout ReadPageLayout(SectionProperties? sectionProperties, WarningCollector warnings)
    {
        if (sectionProperties is null)
            return CloneDefaultPageLayout();

        var pageSize = sectionProperties.GetFirstChild<PageSize>();
        var pageMargin = sectionProperties.GetFirstChild<PageMargin>();
        if (pageSize is null || pageMargin is null)
            warnings.Add("Eksik Word bölüm geometrisi önceki bölümden devralınmıyor; eksik değerlerde KKL A4 varsayılanları kullanılıyor.");

        var width = pageSize?.Width?.Value is uint widthTwips
            ? widthTwips / TwipsPerMillimeter
            : DefaultPageLayout.WidthMillimeters;
        var height = pageSize?.Height?.Value is uint heightTwips
            ? heightTwips / TwipsPerMillimeter
            : DefaultPageLayout.HeightMillimeters;

        if (pageSize?.Orient?.Value == PageOrientationValues.Landscape && width < height)
            (width, height) = (height, width);
        else if (pageSize?.Orient?.Value == PageOrientationValues.Portrait && width > height)
            (width, height) = (height, width);

        if (sectionProperties.Descendants<Columns>().Any(columns => columns.ColumnCount?.Value > 1))
            warnings.Add("Çok sütunlu Word bölümü tek akış sütunu olarak önizleniyor.");

        return new PageLayout
        {
            WidthMillimeters = width,
            HeightMillimeters = height,
            MarginTopMillimeters = pageMargin?.Top?.Value is int top ? top / TwipsPerMillimeter : DefaultPageLayout.MarginTopMillimeters,
            MarginBottomMillimeters = pageMargin?.Bottom?.Value is int bottom ? bottom / TwipsPerMillimeter : DefaultPageLayout.MarginBottomMillimeters,
            MarginLeftMillimeters = pageMargin?.Left?.Value is uint left ? left / TwipsPerMillimeter : DefaultPageLayout.MarginLeftMillimeters,
            MarginRightMillimeters = pageMargin?.Right?.Value is uint right ? right / TwipsPerMillimeter : DefaultPageLayout.MarginRightMillimeters,
            ShowPageNumbers = false
        };
    }

    private static PageLayout CloneDefaultPageLayout() => new()
    {
        WidthMillimeters = DefaultPageLayout.WidthMillimeters,
        HeightMillimeters = DefaultPageLayout.HeightMillimeters,
        MarginTopMillimeters = DefaultPageLayout.MarginTopMillimeters,
        MarginBottomMillimeters = DefaultPageLayout.MarginBottomMillimeters,
        MarginLeftMillimeters = DefaultPageLayout.MarginLeftMillimeters,
        MarginRightMillimeters = DefaultPageLayout.MarginRightMillimeters,
        ShowPageNumbers = DefaultPageLayout.ShowPageNumbers
    };


    private static IEnumerable<OpenXmlElement> SelfAndDescendants(OpenXmlElement element)
    {
        yield return element;
        foreach (var descendant in element.Descendants())
            yield return descendant;
    }

    private static bool ContainsMeaningfulUnsupportedContent(OpenXmlElement element) =>
        SelfAndDescendants(element).Any(candidate => candidate.LocalName is
            "txbxContent" or "txbx" or "shape" or "wsp" or "sp" or "rect" or "oval" or "chart" or "relIds" or
            "oMath" or "oMathPara" or "object" or "control");

    private static string DescribeUnsupported(OpenXmlElement element)
    {
        var descendants = SelfAndDescendants(element).Select(candidate => candidate.LocalName).ToHashSet(StringComparer.Ordinal);
        if (descendants.Contains("txbxContent") || descendants.Contains("txbx") || descendants.Contains("shape") || descendants.Contains("wsp") || descendants.Contains("sp") || descendants.Contains("rect") || descendants.Contains("oval"))
            return "Metin kutusu/VML şekli Word-faithful önizleme kapsamında desteklenmiyor.";
        if (descendants.Contains("chart"))
            return "Word grafiği önizleme sözleşmesi tarafından desteklenmiyor.";
        if (descendants.Contains("relIds"))
            return "SmartArt/diyagram önizleme sözleşmesi tarafından desteklenmiyor.";
        if (descendants.Contains("oMath") || descendants.Contains("oMathPara"))
            return "Word denklemi önizleme sözleşmesi tarafından desteklenmiyor.";
        return $"Word içeriği önizleme sözleşmesi tarafından desteklenmiyor: {element.LocalName}.";
    }

    private static void AddUnsupported(
        ICollection<ImportedDocumentBlock> blocks,
        WarningCollector warnings,
        string description)
    {
        warnings.Add(description);
        blocks.Add(new ImportedUnsupportedBlock { Description = description });
    }

    private sealed class WarningCollector
    {
        private readonly List<string> _items = new();
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public IReadOnlyList<string> Items => _items;

        public void Add(string warning)
        {
            if (_seen.Add(warning))
                _items.Add(warning);
        }
    }

    private sealed class NarrowStyleResolver
    {
        private const string WordprocessingNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private readonly Dictionary<string, Style> _styles;
        private readonly OpenXmlElement? _defaultRunProperties;
        private readonly OpenXmlElement? _defaultParagraphProperties;

        public NarrowStyleResolver(MainDocumentPart mainPart)
        {
            var stylesRoot = mainPart.StyleDefinitionsPart?.Styles;
            _styles = stylesRoot?.Elements<Style>()
                .Where(style => !string.IsNullOrWhiteSpace(style.StyleId?.Value))
                .GroupBy(style => style.StyleId!.Value!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
                ?? new Dictionary<string, Style>(StringComparer.Ordinal);

            var docDefaults = stylesRoot?.ChildElements.FirstOrDefault(element => element.LocalName == "docDefaults");
            _defaultRunProperties = docDefaults?.Descendants().FirstOrDefault(element => element.LocalName == "rPr" && element.Parent?.LocalName == "rPrDefault");
            _defaultParagraphProperties = docDefaults?.Descendants().FirstOrDefault(element => element.LocalName == "pPr" && element.Parent?.LocalName == "pPrDefault");
        }

        public string? GetParagraphStyleId(Paragraph paragraph) =>
            GetVal(paragraph.ParagraphProperties, "pStyle");

        public ParagraphAlignment ResolveParagraphAlignment(Paragraph paragraph, string? paragraphStyleId)
        {
            var value = GetVal(paragraph.ParagraphProperties, "jc")
                        ?? ResolveStyleProperty(paragraphStyleId, "pPr", "jc")
                        ?? GetVal(_defaultParagraphProperties, "jc");

            return value switch
            {
                "center" => ParagraphAlignment.Center,
                "right" => ParagraphAlignment.Right,
                "both" or "distribute" or "thaiDistribute" => ParagraphAlignment.Justify,
                _ => ParagraphAlignment.Left
            };
        }

        public bool ResolveKeepWithNext(Paragraph paragraph, string? paragraphStyleId) =>
            GetOnOff(paragraph.ParagraphProperties, "keepNext")
            ?? ResolveStyleOnOff(paragraphStyleId, "pPr", "keepNext")
            ?? GetOnOff(_defaultParagraphProperties, "keepNext")
            ?? false;

        public bool ResolvePageBreakBefore(Paragraph paragraph, string? paragraphStyleId) =>
            GetOnOff(paragraph.ParagraphProperties, "pageBreakBefore")
            ?? ResolveStyleOnOff(paragraphStyleId, "pPr", "pageBreakBefore")
            ?? GetOnOff(_defaultParagraphProperties, "pageBreakBefore")
            ?? false;

        public RunFormatting ResolveRunFormatting(Run run, string? paragraphStyleId)
        {
            var formatting = new MutableRunFormatting();
            ApplyRunProperties(formatting, _defaultRunProperties);

            foreach (var style in EnumerateStyleChain(paragraphStyleId))
                ApplyRunProperties(formatting, FindDirectChild(style, "rPr"));

            var characterStyleId = GetVal(run.RunProperties, "rStyle");
            foreach (var style in EnumerateStyleChain(characterStyleId))
                ApplyRunProperties(formatting, FindDirectChild(style, "rPr"));

            ApplyRunProperties(formatting, run.RunProperties);

            return new RunFormatting(
                formatting.Bold ?? false,
                formatting.Italic ?? false,
                formatting.Underline ?? false,
                formatting.FontSizePoints ?? 11,
                formatting.FontFamilyName);
        }

        private string? ResolveStyleProperty(string? styleId, string propertyContainer, string propertyName)
        {
            string? value = null;
            foreach (var style in EnumerateStyleChain(styleId))
            {
                var candidate = GetVal(FindDirectChild(style, propertyContainer), propertyName);
                if (candidate is not null)
                    value = candidate;
            }
            return value;
        }

        private bool? ResolveStyleOnOff(string? styleId, string propertyContainer, string propertyName)
        {
            bool? value = null;
            foreach (var style in EnumerateStyleChain(styleId))
            {
                var candidate = GetOnOff(FindDirectChild(style, propertyContainer), propertyName);
                if (candidate.HasValue)
                    value = candidate;
            }
            return value;
        }

        private IEnumerable<Style> EnumerateStyleChain(string? styleId)
        {
            if (string.IsNullOrWhiteSpace(styleId))
                yield break;

            var chain = new Stack<Style>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var currentId = styleId;

            while (!string.IsNullOrWhiteSpace(currentId) && seen.Add(currentId) && _styles.TryGetValue(currentId, out var style))
            {
                chain.Push(style);
                currentId = GetVal(style, "basedOn");
            }

            while (chain.Count > 0)
                yield return chain.Pop();
        }

        private static void ApplyRunProperties(MutableRunFormatting formatting, OpenXmlElement? runProperties)
        {
            if (runProperties is null)
                return;

            formatting.Bold = GetOnOff(runProperties, "b") ?? formatting.Bold;
            formatting.Italic = GetOnOff(runProperties, "i") ?? formatting.Italic;

            var underline = FindDirectChild(runProperties, "u");
            if (underline is not null)
            {
                var value = ReadVal(underline);
                formatting.Underline = !string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
                                       && !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                                       && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
            }

            var fontSize = GetVal(runProperties, "sz");
            if (double.TryParse(fontSize, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var halfPoints))
                formatting.FontSizePoints = halfPoints / 2.0;

            var fonts = FindDirectChild(runProperties, "rFonts");
            formatting.FontFamilyName = GetAttributeValue(fonts, "ascii")
                                        ?? GetAttributeValue(fonts, "hAnsi")
                                        ?? GetAttributeValue(fonts, "eastAsia")
                                        ?? GetAttributeValue(fonts, "cs")
                                        ?? formatting.FontFamilyName;
        }

        private static OpenXmlElement? FindDirectChild(OpenXmlElement? parent, string localName) =>
            parent?.ChildElements.FirstOrDefault(element => element.LocalName == localName);

        private static string? GetVal(OpenXmlElement? parent, string localName) =>
            ReadVal(FindDirectChild(parent, localName));

        private static string? ReadVal(OpenXmlElement? element) =>
            GetAttributeValue(element, "val");

        private static string? GetAttributeValue(OpenXmlElement? element, string localName)
        {
            if (element is null)
                return null;

            var attribute = element.GetAttributes()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.LocalName, localName, StringComparison.Ordinal)
                    && string.Equals(candidate.NamespaceUri, WordprocessingNamespace, StringComparison.Ordinal));

            return string.IsNullOrEmpty(attribute.Value) ? null : attribute.Value;
        }

        private static bool? GetOnOff(OpenXmlElement? parent, string localName)
        {
            var element = FindDirectChild(parent, localName);
            if (element is null)
                return null;

            var value = ReadVal(element);
            return value is null || value is "1" or "true" or "on";
        }

        private sealed class MutableRunFormatting
        {
            public bool? Bold { get; set; }
            public bool? Italic { get; set; }
            public bool? Underline { get; set; }
            public double? FontSizePoints { get; set; }
            public string? FontFamilyName { get; set; }
        }

        public readonly record struct RunFormatting(
            bool Bold,
            bool Italic,
            bool Underline,
            double FontSizePoints,
            string? FontFamilyName);
    }
}
