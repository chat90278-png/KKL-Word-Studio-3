namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.Export.Exporters;
using KKL.WordStudio.Infrastructure.ReferenceFormatting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DomainTableRow = KKL.WordStudio.Domain.Elements.TableRow;
using OpenXmlTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

public sealed class Sprint18CanonicalGeneratedDocxFidelityTests
{
    private const double TwipsPerMillimeter = 1440d / 25.4d;

    [Fact]
    public async Task ProductPipeline_GeneratesCanonicalA4HeadingsCaptionAndGroupedLongTableDocx()
    {
        var (project, report, tableElement) = CreateCanonicalReport();
        var builder = CreateBuilder();

        var semanticDocument = await builder.BuildAsync(project, report);
        var semanticTable = Assert.IsType<TableContentNode>(
            semanticDocument.BodyNodes.Single(node => node.ElementId == tableElement.Id));
        Assert.Equal(70, semanticTable.Rows.Count);
        Assert.Equal("SER-A", semanticTable.Rows[21][4]);
        Assert.Equal("SER-B", semanticTable.Rows[22][4]);
        Assert.Equal("2", semanticTable.Rows[21][5]);
        Assert.Equal(string.Empty, semanticTable.Rows[22][5]);
        Assert.Equal(5, semanticTable.CellSpans.Count(span => span.RowIndex == 21 && span.RowSpan == 2));
        Assert.DoesNotContain(semanticTable.CellSpans, span => span.RowIndex == 21 && span.ColumnIndex == 4);
        var boundaryGroup = Assert.Single(semanticTable.RowGroups, group => group.StartRowIndex == 21);
        Assert.Equal(2, boundaryGroup.RowCount);
        Assert.True(boundaryGroup.KeepTogetherWhenPossible);

        var exporter = new WordExporter(builder, NullLogger<WordExporter>.Instance);
        var export = await exporter.ExportAsync(project, report, ExportOptions.Default);
        Assert.True(export.IsSuccess, export.Error);
        await using var stream = export.Value;
        stream.Position = 0;

        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart!.Document.Body!;
        var profile = DefaultDocumentFormatProfileFactory.Create();

        AssertPageGeometry(body, profile);
        AssertParagraphFormatting(body);
        AssertAutomaticCaption(body);
        AssertCanonicalTable(body, profile);
    }

    private static void AssertPageGeometry(Body body, DocumentFormatProfile profile)
    {
        var section = Assert.IsType<SectionProperties>(body.GetFirstChild<SectionProperties>());
        var pageSize = Assert.IsType<PageSize>(section.GetFirstChild<PageSize>());
        Assert.Equal(ToTwips(profile.Page.WidthMillimeters), pageSize.Width!.Value);
        Assert.Equal(ToTwips(profile.Page.HeightMillimeters), pageSize.Height!.Value);
        Assert.Equal(PageOrientationValues.Portrait, pageSize.Orient!.Value);

        var margin = Assert.IsType<PageMargin>(section.GetFirstChild<PageMargin>());
        Assert.Equal((int)ToTwips(profile.Page.MarginTopMillimeters), margin.Top!.Value);
        Assert.Equal((int)ToTwips(profile.Page.MarginBottomMillimeters), margin.Bottom!.Value);
        Assert.Equal(ToTwips(profile.Page.MarginLeftMillimeters), margin.Left!.Value);
        Assert.Equal(ToTwips(profile.Page.MarginRightMillimeters), margin.Right!.Value);
        Assert.Equal(ToTwips(profile.Page.HeaderDistanceMillimeters), margin.Header!.Value);
        Assert.Equal(ToTwips(profile.Page.FooterDistanceMillimeters), margin.Footer!.Value);
    }

    private static void AssertParagraphFormatting(Body body)
    {
        var primary = FindParagraph(body, "Primary Heading");
        Assert.Equal("Heading1", primary.ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        Assert.NotNull(primary.ParagraphProperties.GetFirstChild<KeepNext>());
        AssertRun(primary, "Arial", "24", bold: true, italic: true);

        var secondary = FindParagraph(body, "Secondary Heading");
        Assert.Equal("Heading2", secondary.ParagraphProperties!.ParagraphStyleId!.Val!.Value);
        Assert.NotNull(secondary.ParagraphProperties.GetFirstChild<KeepNext>());
        AssertRun(secondary, "Arial", "24", bold: true, italic: false);
        Assert.Equal(
            ((int)ToTwips(21.75d)).ToString(),
            secondary.ParagraphProperties.GetFirstChild<Indentation>()!.Left!.Value);

        var bodyParagraph = FindParagraph(body, "Canonical body paragraph");
        AssertRun(bodyParagraph, "Arial", "20", bold: false, italic: false);
    }

    private static void AssertAutomaticCaption(Body body)
    {
        var caption = body.Elements<Paragraph>()
            .Single(paragraph => paragraph.Descendants<SimpleField>().Any());
        var field = Assert.Single(caption.Descendants<SimpleField>());
        var instruction = field.Instruction?.Value;
        Assert.NotNull(instruction);
        Assert.Contains("SEQ Tablo", instruction, StringComparison.Ordinal);
        Assert.Contains("ARABIC", instruction, StringComparison.Ordinal);
        Assert.Equal("1", field.InnerText);
        Assert.Equal("Tablo 1: Canonical grouped long table", caption.InnerText);
        Assert.Equal(
            JustificationValues.Center,
            caption.ParagraphProperties!.GetFirstChild<Justification>()!.Val!.Value);
        Assert.NotNull(caption.ParagraphProperties.GetFirstChild<KeepNext>());
        AssertRun(caption, "Arial", "16", bold: true, italic: false);
    }

    private static void AssertCanonicalTable(Body body, DocumentFormatProfile profile)
    {
        var table = Assert.Single(body.Elements<OpenXmlTable>());
        var properties = Assert.IsType<TableProperties>(table.GetFirstChild<TableProperties>());
        Assert.Equal(TableLayoutValues.Fixed, properties.GetFirstChild<TableLayout>()!.Type!.Value);
        Assert.Equal(TableWidthUnitValues.Pct, properties.GetFirstChild<TableWidth>()!.Type!.Value);
        Assert.Equal("5000", properties.GetFirstChild<TableWidth>()!.Width!.Value);

        var borders = properties.GetFirstChild<TableBorders>()!;
        Assert.Equal(4u, borders.TopBorder!.Size!.Value);
        Assert.Equal(4u, borders.BottomBorder!.Size!.Value);
        Assert.Equal(4u, borders.InsideHorizontalBorder!.Size!.Value);
        Assert.Equal(4u, borders.InsideVerticalBorder!.Size!.Value);

        var margins = properties.GetFirstChild<TableCellMarginDefault>()!;
        Assert.Equal(
            (short)ToTwips(profile.TableFormats[0].Format.CellMarginLeftMillimeters),
            margins.GetFirstChild<TableCellLeftMargin>()!.Width!.Value);
        Assert.Equal(
            (short)ToTwips(profile.TableFormats[0].Format.CellMarginRightMillimeters),
            margins.GetFirstChild<TableCellRightMargin>()!.Width!.Value);

        var gridWidths = table.GetFirstChild<TableGrid>()!
            .Elements<GridColumn>()
            .Select(ParseGridWidth)
            .ToArray();
        Assert.Equal(6, gridWidths.Length);
        Assert.True(gridWidths.Distinct().Count() > 1);
        Assert.True(gridWidths[1] > gridWidths[0]);

        var rows = table.Elements<OpenXmlTableRow>().ToArray();
        Assert.Equal(71, rows.Length);
        var headerProperties = rows[0].TableRowProperties!;
        Assert.NotNull(headerProperties.GetFirstChild<TableHeader>());
        var headerHeight = headerProperties.GetFirstChild<TableRowHeight>()!;
        Assert.Equal(ToTwips(profile.TableFormats[0].Format.PreferredRowHeightMillimeters), headerHeight.Val!.Value);
        Assert.Equal(HeightRuleValues.AtLeast, headerHeight.HeightType!.Value);

        var dataRows = rows.Skip(1).ToArray();
        var firstBoundaryRowIndex = Array.FindIndex(dataRows, row => GetCell(row, 4).InnerText == "SER-A");
        Assert.True(firstBoundaryRowIndex >= 0);
        Assert.True(firstBoundaryRowIndex + 1 < dataRows.Length);
        var firstBoundaryRow = dataRows[firstBoundaryRowIndex];
        var secondBoundaryRow = dataRows[firstBoundaryRowIndex + 1];
        Assert.Equal("SER-B", GetCell(secondBoundaryRow, 4).InnerText);

        foreach (var columnIndex in new[] { 0, 1, 2, 3, 5 })
        {
            Assert.Equal(
                MergedCellValues.Restart,
                GetCell(firstBoundaryRow, columnIndex).TableCellProperties!
                    .GetFirstChild<VerticalMerge>()!.Val!.Value);
            Assert.Equal(
                MergedCellValues.Continue,
                GetCell(secondBoundaryRow, columnIndex).TableCellProperties!
                    .GetFirstChild<VerticalMerge>()!.Val!.Value);
        }

        Assert.Null(GetCell(firstBoundaryRow, 4).TableCellProperties!.GetFirstChild<VerticalMerge>());
        Assert.Null(GetCell(secondBoundaryRow, 4).TableCellProperties!.GetFirstChild<VerticalMerge>());
        Assert.Equal("2", GetCell(firstBoundaryRow, 5).InnerText);
        Assert.Equal(string.Empty, GetCell(secondBoundaryRow, 5).InnerText);
    }

    private static Paragraph FindParagraph(Body body, string text) =>
        Assert.Single(body.Elements<Paragraph>(), paragraph => paragraph.InnerText == text);

    private static void AssertRun(
        Paragraph paragraph,
        string fontFamily,
        string halfPoints,
        bool bold,
        bool italic)
    {
        var properties = paragraph.Descendants<Run>().First().RunProperties!;
        Assert.Equal(fontFamily, properties.GetFirstChild<RunFonts>()!.Ascii!.Value);
        Assert.Equal(halfPoints, properties.GetFirstChild<FontSize>()!.Val!.Value);
        Assert.Equal(bold, properties.GetFirstChild<Bold>()!.Val!.Value);
        Assert.Equal(italic, properties.GetFirstChild<Italic>()!.Val!.Value);
    }

    private static TableCell GetCell(OpenXmlTableRow row, int columnIndex) =>
        row.Elements<TableCell>().ElementAt(columnIndex);

    private static uint ParseGridWidth(GridColumn column)
    {
        var width = column.Width?.Value;
        Assert.NotNull(width);
        return uint.Parse(width);
    }

    private static uint ToTwips(double millimeters) =>
        (uint)Math.Round(millimeters * TwipsPerMillimeter);

    private static (Project Project, Report Report, TableElement Table) CreateCanonicalReport()
    {
        var project = new Project { Name = "Sprint18 canonical project" };
        var report = new Report { Name = "Sprint18 canonical report" };
        var page = new Page();
        var body = new Section { Name = "Body", Kind = SectionKind.Body };

        body.Root.Children.Add(new TextElement
        {
            Name = "Primary heading",
            Content = Expression.Literal("Primary Heading"),
            Style = HeadingStylePresets.CreateHeadingStyle()
        });
        body.Root.Children.Add(new TextElement
        {
            Name = "Secondary heading",
            Content = Expression.Literal("Secondary Heading"),
            Style = HeadingStylePresets.CreateAltHeadingStyle()
        });
        body.Root.Children.Add(new TextElement
        {
            Name = "Body paragraph",
            Content = Expression.Literal("Canonical body paragraph")
        });

        var table = CreateLongGroupedTable();
        body.Root.Children.Add(table);
        page.Sections.Add(body);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report, table);
    }

    private static TableElement CreateLongGroupedTable()
    {
        var table = new TableElement
        {
            Name = "Canonical long table",
            Caption = "Canonical grouped long table"
        };
        var headers = new[] { "No", "Product Name", "Product Number", "NSN", "Serial No", "Quantity" };
        foreach (var header in headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });

        table.SerialQuantityGrouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = table.Columns[2].Id,
            SerialNumberColumnId = table.Columns[4].Id,
            QuantityColumnId = table.Columns[5].Id,
            WasAutoDetected = false
        };

        for (var index = 0; index < 70; index++)
        {
            if (index == 21)
            {
                AddDetailRow(table, "22", "Boundary Product", "P-022", "NSN-022", "SER-A", "1");
                continue;
            }

            if (index == 22)
            {
                AddDetailRow(table, string.Empty, "Boundary Product", "P-022", string.Empty, "SER-B", "1");
                continue;
            }

            AddDetailRow(
                table,
                (index + 1).ToString(),
                $"Product {index + 1}",
                $"P-{index + 1:000}",
                $"NSN-{index + 1:000}",
                $"SER-{index + 1:000}",
                "1");
        }

        return table;
    }

    private static void AddDetailRow(TableElement table, params string[] values)
    {
        var row = new DomainTableRow { Kind = TableRowKind.Detail };
        foreach (var value in values)
        {
            var cell = new Container();
            cell.Children.Add(new TextElement { Content = Expression.Literal(value) });
            row.Cells.Add(cell);
        }
        table.Rows.Add(row);
    }

    private static ReportContentBuilder CreateBuilder() =>
        new(
            new NoOpRegistry(),
            new SerialQuantityTableContentRowComposer(),
            new OpenXmlReferenceDocumentFormatProvider(),
            new ReferenceReportContentFormatResolver());

    private sealed class NoOpRegistry : IDataProviderRegistry
    {
        public void Register(IDataProvider provider) { }

        public IDataProvider Resolve(string providerKey) =>
            throw new InvalidOperationException(
                $"Canonical static-table fixture must not resolve provider '{providerKey}'.");
    }
}
