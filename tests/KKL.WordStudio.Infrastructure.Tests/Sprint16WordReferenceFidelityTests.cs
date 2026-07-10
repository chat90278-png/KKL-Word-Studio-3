namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Xunit;
using OpenXmlTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

public sealed class Sprint16WordReferenceFidelityTests
{
    [Fact]
    public void SeroWord_Page_WritesA4And25MmMargins()
    {
        var section = BuildSectionProperties();
        var size = section.GetFirstChild<PageSize>()!;
        var margin = section.GetFirstChild<PageMargin>()!;

        Assert.Equal(11906u, size.Width!.Value);
        Assert.Equal(16838u, size.Height!.Value);
        Assert.Equal(PageOrientationValues.Portrait, size.Orient!.Value);
        Assert.Equal(1417, margin.Top!.Value);
        Assert.Equal(1417, margin.Bottom!.Value);
        Assert.Equal(1417u, margin.Left!.Value);
        Assert.Equal(1417u, margin.Right!.Value);
    }

    [Fact]
    public void SeroWord_Page_WritesHeaderFooterDistance()
    {
        var margin = BuildSectionProperties().GetFirstChild<PageMargin>()!;

        Assert.Equal(708u, margin.Header!.Value);
        Assert.Equal(708u, margin.Footer!.Value);
    }

    [Fact]
    public void SeroWord_PrimaryHeading_WritesArial12BoldItalicSpacingKeepNext()
    {
        var paragraph = WordParagraphWriter.BuildParagraph(CreateTextNode(
            ReportContentKind.Heading,
            CreateTextFormat(
                fontSize: 12d,
                bold: true,
                italic: true,
                spaceBefore: 4d,
                spaceAfter: 2d,
                keepWithNext: true)));
        var properties = paragraph.ParagraphProperties!;
        var runProperties = paragraph.GetFirstChild<Run>()!.RunProperties!;

        Assert.Equal("Heading1", properties.ParagraphStyleId!.Val!.Value);
        Assert.NotNull(properties.GetFirstChild<KeepNext>());
        Assert.Equal("80", properties.SpacingBetweenLines!.Before!.Value);
        Assert.Equal("40", properties.SpacingBetweenLines.After!.Value);
        Assert.Equal("240", properties.SpacingBetweenLines.Line!.Value);
        Assert.Equal(LineSpacingRuleValues.Auto, properties.SpacingBetweenLines.LineRule!.Value);
        Assert.Equal("Arial", runProperties.RunFonts!.Ascii!.Value);
        Assert.Equal("Arial", runProperties.RunFonts.HighAnsi!.Value);
        Assert.Equal("24", runProperties.FontSize!.Val!.Value);
        Assert.True(runProperties.Bold!.Val!.Value);
        Assert.True(runProperties.Italic!.Val!.Value);
        Assert.Equal("000000", runProperties.Color!.Val!.Value);
        Assert.Equal(UnderlineValues.None, runProperties.Underline!.Val!.Value);
    }

    [Fact]
    public void SeroWord_SecondaryHeading_WritesReferenceIndent()
    {
        var paragraph = WordParagraphWriter.BuildParagraph(CreateTextNode(
            ReportContentKind.AltHeading,
            CreateTextFormat(
                fontSize: 12d,
                bold: true,
                leftIndent: 21.74875d,
                keepWithNext: true)));

        Assert.Equal("Heading2", paragraph.ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        Assert.Equal("1233", paragraph.ParagraphProperties.Indentation!.Left!.Value);
        Assert.Equal("0", paragraph.ParagraphProperties.Indentation.FirstLine!.Value);
    }

    [Fact]
    public void SeroWord_CenteredIdentifier_WritesCenterBold11()
    {
        var paragraph = WordParagraphWriter.BuildParagraph(CreateTextNode(
            ReportContentKind.Paragraph,
            CreateTextFormat(
                fontSize: 11d,
                bold: true,
                alignment: ParagraphAlignment.Center)));
        var runProperties = paragraph.GetFirstChild<Run>()!.RunProperties!;

        Assert.Equal(JustificationValues.Center, paragraph.ParagraphProperties!.Justification!.Val!.Value);
        Assert.Equal("22", runProperties.FontSize!.Val!.Value);
        Assert.True(runProperties.Bold!.Val!.Value);
        Assert.Equal("Arial", runProperties.RunFonts!.Ascii!.Value);
    }

    [Fact]
    public void SeroWord_Caption_WritesCenteredReferenceFormat()
    {
        var captionFormat = CreateTextFormat(
            fontSize: 8d,
            bold: true,
            alignment: ParagraphAlignment.Center,
            keepWithNext: true,
            lineSpacingMultiple: 2d,
            firstLineIndentMillimeters: 12.488d);
        var body = new Body();

        WordContentWriter.AppendNode(body, CreateTableNode(captionFormat: captionFormat));

        var caption = body.Elements<Paragraph>().First();
        var properties = Assert.IsType<ParagraphProperties>(caption.ParagraphProperties);
        Assert.Equal(JustificationValues.Center, properties.Justification!.Val!.Value);
        Assert.NotNull(properties.GetFirstChild<KeepNext>());
        var spacing = Assert.IsType<SpacingBetweenLines>(properties.GetFirstChild<SpacingBetweenLines>());
        Assert.Equal("480", spacing.Line!.Value);
        Assert.Equal(LineSpacingRuleValues.Auto, spacing.LineRule!.Value);
        var indent = Assert.IsType<Indentation>(properties.GetFirstChild<Indentation>());
        var firstLineValue = Assert.IsType<string>(indent.FirstLine!.Value);
        Assert.InRange(int.Parse(firstLineValue), 707, 709);

        var runs = caption.Descendants<Run>().ToArray();
        Assert.NotEmpty(runs);
        Assert.All(runs, run =>
        {
            var runProperties = Assert.IsType<RunProperties>(run.RunProperties);
            Assert.Equal("Arial", runProperties.RunFonts!.Ascii!.Value);
            Assert.Equal("16", runProperties.FontSize!.Val!.Value);
            Assert.True(runProperties.Bold!.Val!.Value);
        });
        var field = Assert.Single(caption.Descendants<SimpleField>());
        var fieldRun = Assert.Single(field.Elements<Run>());
        Assert.NotNull(fieldRun.RunProperties);
        Assert.Contains("Unmanned Aerial Vehicle", caption.InnerText, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvedHeadingKeepWithNextFalse_IsNotForcedByWord()
    {
        var withoutKeep = WordParagraphWriter.BuildParagraph(CreateTextNode(
            ReportContentKind.Heading,
            CreateTextFormat(fontSize: 12d, bold: true, keepWithNext: false)));
        Assert.Equal("Heading1", withoutKeep.ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        Assert.Null(withoutKeep.ParagraphProperties.GetFirstChild<KeepNext>());

        var withKeep = WordParagraphWriter.BuildParagraph(CreateTextNode(
            ReportContentKind.Heading,
            CreateTextFormat(fontSize: 12d, bold: true, keepWithNext: true)));
        Assert.Equal("Heading1", withKeep.ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        Assert.NotNull(withKeep.ParagraphProperties.GetFirstChild<KeepNext>());
    }

    [Fact]
    public void SeroWord_Caption_WritesRealSeqField()
    {
        var body = new Body();
        WordContentWriter.AppendNode(body, CreateTableNode(caption: "Table 7. Unmanned Aerial Vehicle"));
        var caption = body.Elements<Paragraph>().First();
        var field = Assert.Single(caption.Descendants<SimpleField>());

        Assert.Contains("SEQ Tablo", field.Instruction!.Value, StringComparison.Ordinal);
        Assert.Contains("ARABIC", field.Instruction.Value, StringComparison.Ordinal);
        Assert.StartsWith("Table ", caption.InnerText);
        Assert.Contains(". Unmanned Aerial Vehicle", caption.InnerText, StringComparison.Ordinal);
        Assert.DoesNotContain("Table 1. Table 7.", caption.InnerText, StringComparison.Ordinal);
    }

    [Fact]
    public void SeroWord_Table_WritesFixedFullWidthAndHalfPointBorders()
    {
        var table = WordTableWriter.BuildTable(CreateTableNode());
        var properties = table.GetFirstChild<TableProperties>()!;
        var borders = properties.GetFirstChild<TableBorders>()!;

        Assert.Equal("5000", properties.GetFirstChild<TableWidth>()!.Width!.Value);
        Assert.Equal(TableWidthUnitValues.Pct, properties.GetFirstChild<TableWidth>()!.Type!.Value);
        Assert.Equal(TableLayoutValues.Fixed, properties.GetFirstChild<TableLayout>()!.Type!.Value);
        Assert.Equal(4u, borders.TopBorder!.Size!.Value);
        Assert.Equal(4u, borders.BottomBorder!.Size!.Value);
        Assert.Equal(4u, borders.LeftBorder!.Size!.Value);
        Assert.Equal(4u, borders.RightBorder!.Size!.Value);
        Assert.Equal(4u, borders.InsideHorizontalBorder!.Size!.Value);
        Assert.Equal(4u, borders.InsideVerticalBorder!.Size!.Value);
    }

    [Fact]
    public void SeroWord_Table_WritesReferenceCellMargins()
    {
        var margins = WordTableWriter.BuildTable(CreateTableNode())
            .GetFirstChild<TableProperties>()!
            .GetFirstChild<TableCellMarginDefault>()!;

        Assert.Equal("0", margins.GetFirstChild<TopMargin>()!.Width!.Value);
        Assert.Equal("0", margins.GetFirstChild<BottomMargin>()!.Width!.Value);
        Assert.Equal((short)70, margins.GetFirstChild<TableCellLeftMargin>()!.Width!.Value);
        Assert.Equal((short)70, margins.GetFirstChild<TableCellRightMargin>()!.Width!.Value);
    }

    [Fact]
    public void SeroWord_Table_WritesPreferredRowHeight()
    {
        var table = WordTableWriter.BuildTable(CreateTableNode());
        var heights = table.Elements<OpenXmlTableRow>()
            .Select(row => row.TableRowProperties!.GetFirstChild<TableRowHeight>())
            .ToArray();

        Assert.All(heights, height => Assert.NotNull(height));
        Assert.All(heights, height => Assert.Equal(578u, height!.Val!.Value));
        Assert.All(heights, height => Assert.Equal(HeightRuleValues.AtLeast, height!.HeightType!.Value));
    }

    [Fact]
    public void SeroWord_Table_WritesUnequalGridWidths()
    {
        var table = WordTableWriter.BuildTable(CreateTableNode());
        var widths = table.GetFirstChild<TableGrid>()!
            .Elements<GridColumn>()
            .Select(column => uint.Parse(Assert.IsType<string>(column.Width!.Value)))
            .ToArray();
        var cellWidths = table.Elements<OpenXmlTableRow>().First()
            .Elements<TableCell>()
            .Select(cell => int.Parse(Assert.IsType<string>(
                cell.TableCellProperties!.GetFirstChild<TableCellWidth>()!.Width!.Value)))
            .ToArray();

        Assert.Equal(6, widths.Length);
        Assert.True(widths.Distinct().Count() > 3);
        Assert.True(widths[1] > widths[0]);
        Assert.True(widths[4] > widths[5]);
        Assert.True(cellWidths.Distinct().Count() > 3);
    }

    [Fact]
    public void SeroWord_Table_ProductNameLeftOthersCentered()
    {
        var table = WordTableWriter.BuildTable(CreateTableNode());
        var header = table.Elements<OpenXmlTableRow>().First();
        var data = table.Elements<OpenXmlTableRow>().Skip(1).First();

        Assert.Equal(JustificationValues.Left, GetCellJustification(header, 1));
        Assert.Equal(JustificationValues.Left, GetCellJustification(data, 1));
        foreach (var columnIndex in new[] { 0, 2, 3, 4, 5 })
        {
            Assert.Equal(JustificationValues.Center, GetCellJustification(header, columnIndex));
            Assert.Equal(JustificationValues.Center, GetCellJustification(data, columnIndex));
        }

        Assert.All(data.Elements<TableCell>(), cell => Assert.NotNull(cell.TableCellProperties!.GetFirstChild<NoWrap>()));
    }

    [Fact]
    public void SeroWord_Table_HeaderRepeats()
    {
        var header = WordTableWriter.BuildTable(CreateTableNode()).Elements<OpenXmlTableRow>().First();

        Assert.NotNull(header.TableRowProperties!.GetFirstChild<TableHeader>());
    }

    [Fact]
    public void SeroWord_Table_GroupedSerial_PreservesTrueVMerge()
    {
        var table = WordTableWriter.BuildTable(CreateTableNode(grouped: true));

        foreach (var columnIndex in new[] { 0, 1, 2, 3, 5 })
        {
            Assert.Equal(MergedCellValues.Restart, GetMerge(table, 0, columnIndex)!.Val!.Value);
            Assert.Equal(MergedCellValues.Continue, GetMerge(table, 1, columnIndex)!.Val!.Value);
        }
    }

    [Fact]
    public void SeroWord_Table_SerialColumn_RemainsUnmerged()
    {
        var table = WordTableWriter.BuildTable(CreateTableNode(grouped: true));

        Assert.Null(GetMerge(table, 0, 4));
        Assert.Null(GetMerge(table, 1, 4));
        Assert.Equal("A222", GetDataCell(table, 0, 4).InnerText);
        Assert.Equal("A221", GetDataCell(table, 1, 4).InnerText);
    }

    [Fact]
    public void SeroWord_GeneratedDocx_ReopensWithExpectedReferenceProperties()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, autoSave: false))
        {
            var mainPart = document.AddMainDocumentPart();
            var documentBody = new Body();
            mainPart.Document = new Document(documentBody);
            documentBody.AppendChild(WordParagraphWriter.BuildParagraph(CreateTextNode(
                ReportContentKind.Heading,
                CreateTextFormat(fontSize: 12d, bold: true, italic: true, spaceBefore: 4d, spaceAfter: 2d, keepWithNext: true))));
            WordContentWriter.AppendNode(documentBody, CreateTableNode(grouped: true));
            var section = new SectionProperties();
            WordPageLayoutWriter.AppendPageLayout(section, CreatePageLayout());
            documentBody.AppendChild(section);
            mainPart.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var body = reopened.MainDocumentPart!.Document.Body!;
        var heading = body.Elements<Paragraph>().First();
        var caption = body.Elements<Paragraph>().Skip(1).First();
        var table = body.GetFirstChild<OpenXmlTable>()!;
        var pageMargin = body.GetFirstChild<SectionProperties>()!.GetFirstChild<PageMargin>()!;

        Assert.Equal("Arial", heading.GetFirstChild<Run>()!.RunProperties!.RunFonts!.Ascii!.Value);
        Assert.NotNull(heading.ParagraphProperties!.GetFirstChild<KeepNext>());
        Assert.Contains("SEQ Tablo", Assert.Single(caption.Descendants<SimpleField>()).Instruction!.Value, StringComparison.Ordinal);
        Assert.Equal("5000", table.GetFirstChild<TableProperties>()!.GetFirstChild<TableWidth>()!.Width!.Value);
        Assert.Equal(MergedCellValues.Restart, GetMerge(table, 0, 0)!.Val!.Value);
        Assert.Equal(708u, pageMargin.Header!.Value);
        Assert.Equal(708u, pageMargin.Footer!.Value);
    }

    private static SectionProperties BuildSectionProperties()
    {
        var section = new SectionProperties();
        WordPageLayoutWriter.AppendPageLayout(section, CreatePageLayout());
        return section;
    }

    private static PageLayout CreatePageLayout() => new()
    {
        WidthMillimeters = 210.009d,
        HeightMillimeters = 297.004d,
        MarginTopMillimeters = 24.994d,
        MarginBottomMillimeters = 24.994d,
        MarginLeftMillimeters = 24.994d,
        MarginRightMillimeters = 24.994d,
        HeaderDistanceMillimeters = 12.488d,
        FooterDistanceMillimeters = 12.488d,
        ShowPageNumbers = true
    };

    private static TextContentNode CreateTextNode(ReportContentKind kind, ResolvedTextFormat format) => new()
    {
        ElementId = Guid.NewGuid(),
        Kind = kind,
        Text = "System Component Configuration List",
        Bold = format.Bold,
        FontSize = format.FontSizePoints,
        Format = format
    };

    private static ResolvedTextFormat CreateTextFormat(
        double fontSize,
        bool bold,
        bool italic = false,
        ParagraphAlignment alignment = ParagraphAlignment.Left,
        double spaceBefore = 0d,
        double spaceAfter = 0d,
        double leftIndent = 0d,
        bool keepWithNext = false,
        double lineSpacingMultiple = 1d,
        double firstLineIndentMillimeters = 0d) => new()
    {
        FontFamilyName = "Arial",
        FontSizePoints = fontSize,
        Bold = bold,
        Italic = italic,
        Underline = false,
        ForegroundColor = "#FF000000",
        Alignment = alignment,
        SpaceBeforePoints = spaceBefore,
        SpaceAfterPoints = spaceAfter,
        LineSpacingMultiple = lineSpacingMultiple,
        LeftIndentMillimeters = leftIndent,
        FirstLineIndentMillimeters = firstLineIndentMillimeters,
        KeepWithNext = keepWithNext
    };

    private static TableContentNode CreateTableNode(
        bool grouped = false,
        string? caption = "Unmanned Aerial Vehicle",
        ResolvedTextFormat? captionFormat = null)
    {
        IReadOnlyList<IReadOnlyList<string>> rows = grouped
            ?
            [
                ["1", "Elma", "1234", "321", "A222", "2"],
                [string.Empty, string.Empty, string.Empty, string.Empty, "A221", string.Empty]
            ]
            :
            [
                ["1", "Elma", "1234", "321", "A222", "2"]
            ];
        IReadOnlyList<TableCellSpan> spans = grouped
            ?
            [
                Span(0, 0, 2),
                Span(0, 1, 2),
                Span(0, 2, 2),
                Span(0, 3, 2),
                Span(0, 5, 2)
            ]
            : Array.Empty<TableCellSpan>();

        return new TableContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.Table,
            Name = "Reference Table 1",
            Caption = caption,
            CaptionFormat = captionFormat,
            CaptionSequence = new TableCaptionSequenceProfile
            {
                DisplayLabel = "Table",
                SequenceIdentifier = "Tablo",
                Separator = ". "
            },
            ColumnHeaders = ["No", "Product Name", "Product Number", "NSN", "Serial No", "Quantity"],
            Rows = rows,
            CellSpans = spans,
            Format = CreateTableFormat(),
            DataSourceName = null,
            SourceCount = 0,
            SourceError = null,
            FilterWasIgnored = false
        };
    }

    private static ResolvedTableFormat CreateTableFormat()
    {
        var weights = new[] { 465d, 2722d, 1404d, 1661d, 1910d, 900d };
        return new ResolvedTableFormat
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
            Columns = weights.Select((weight, index) => new ResolvedTableColumnFormat
            {
                WidthWeight = weight,
                HeaderAlignment = index == 1 ? ParagraphAlignment.Left : ParagraphAlignment.Center,
                BodyAlignment = index == 1 ? ParagraphAlignment.Left : ParagraphAlignment.Center,
                HeaderFontFamilyName = "Arial",
                HeaderFontSizePoints = index == 0 ? 9d : 10d,
                HeaderBold = true,
                BodyFontFamilyName = "Arial",
                BodyFontSizePoints = index == 0 ? 9d : index == 3 ? 12d : 10d,
                BodyBold = false,
                VerticalAlignment = VerticalContentAlignment.Center,
                NoWrap = true
            }).ToArray()
        };
    }

    private static JustificationValues GetCellJustification(OpenXmlTableRow row, int columnIndex) =>
        row.Elements<TableCell>()
            .ElementAt(columnIndex)
            .GetFirstChild<Paragraph>()!
            .ParagraphProperties!
            .Justification!
            .Val!
            .Value;

    private static TableCell GetDataCell(OpenXmlTable table, int rowIndex, int columnIndex) =>
        table.Elements<OpenXmlTableRow>()
            .Skip(1)
            .ElementAt(rowIndex)
            .Elements<TableCell>()
            .ElementAt(columnIndex);

    private static VerticalMerge? GetMerge(OpenXmlTable table, int rowIndex, int columnIndex) =>
        GetDataCell(table, rowIndex, columnIndex)
            .TableCellProperties!
            .GetFirstChild<VerticalMerge>();

    private static TableCellSpan Span(int rowIndex, int columnIndex, int rowSpan) => new()
    {
        RowIndex = rowIndex,
        ColumnIndex = columnIndex,
        RowSpan = rowSpan
    };
}
