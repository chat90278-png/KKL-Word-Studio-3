namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Xunit;
using OpenXmlTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

public sealed class Sprint17SerialQuantityWordPipelineTests
{
    [Fact]
    public void UnitQuantityPhysicalSerialRows_ReachWordAsTrueVerticalMerges()
    {
        var tableElement = CreateConfiguredTable(
            "No",
            "Tr İsim",
            "Parça Numarası",
            "NSN",
            "Seri Numarası",
            "Adet");
        var composition = new SerialQuantityTableContentRowComposer().Compose(tableElement,
        [
            ["2", "armut", "56789", "459-485-5", "9987", "1"],
            ["", "armut", "56789", "", "9988", "1"]
        ]);
        var node = new TableContentNode
        {
            ElementId = tableElement.Id,
            Kind = ReportContentKind.Table,
            Name = "Real serial source shape",
            Caption = null,
            ColumnHeaders = tableElement.Columns.Select(column => column.Header).ToArray(),
            Rows = composition.Rows,
            CellSpans = composition.CellSpans,
            RowGroups = composition.RowGroups,
            CompositionWarnings = composition.Warnings,
            DataSourceName = null,
            SourceCount = 0,
            SourceError = null,
            FilterWasIgnored = false
        };

        var table = RoundTripTable(node);
        var dataRows = table.Elements<OpenXmlTableRow>().Skip(1).ToArray();

        Assert.Equal(2, dataRows.Length);
        Assert.All(dataRows, row => Assert.Equal(6, row.Elements<TableCell>().Count()));

        foreach (var columnIndex in new[] { 0, 1, 2, 3, 5 })
        {
            Assert.Equal(MergedCellValues.Restart, GetMerge(table, 0, columnIndex)!.Val!.Value);
            Assert.Equal(MergedCellValues.Continue, GetMerge(table, 1, columnIndex)!.Val!.Value);
        }

        Assert.Null(GetMerge(table, 0, 4));
        Assert.Null(GetMerge(table, 1, 4));
        Assert.Equal("9987", GetDataCell(table, 0, 4).InnerText);
        Assert.Equal("9988", GetDataCell(table, 1, 4).InnerText);
        Assert.Equal("2", GetDataCell(table, 0, 5).InnerText);
        Assert.Equal(string.Empty, GetDataCell(table, 1, 5).InnerText);
        Assert.Contains("w:vMerge w:val=\"restart\"", table.OuterXml, StringComparison.Ordinal);
        Assert.Contains("w:vMerge w:val=\"continue\"", table.OuterXml, StringComparison.Ordinal);
        Assert.Empty(composition.Warnings);
    }

    private static TableElement CreateConfiguredTable(params string[] headers)
    {
        var table = new TableElement { Name = "Sprint17 Word pipeline table" };
        foreach (var header in headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });

        table.SerialQuantityGrouping = new SerialQuantityGroupingDetector().Detect(table.Columns);
        Assert.NotNull(table.SerialQuantityGrouping);
        return table;
    }

    private static OpenXmlTable RoundTripTable(TableContentNode node)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, autoSave: false))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(WordTableWriter.BuildTable(node)));
            mainPart.Document.Save();
        }

        stream.Position = 0;
        using var reopened = WordprocessingDocument.Open(stream, false);
        var table = reopened.MainDocumentPart!.Document.Body!.GetFirstChild<OpenXmlTable>()!;
        return (OpenXmlTable)table.CloneNode(true);
    }

    private static TableCell GetDataCell(OpenXmlTable table, int rowIndex, int columnIndex) =>
        table.Elements<OpenXmlTableRow>()
            .Skip(1)
            .ElementAt(rowIndex)
            .Elements<TableCell>()
            .ElementAt(columnIndex);

    private static VerticalMerge? GetMerge(OpenXmlTable table, int rowIndex, int columnIndex) =>
        GetDataCell(table, rowIndex, columnIndex).TableCellProperties!.GetFirstChild<VerticalMerge>();
}
