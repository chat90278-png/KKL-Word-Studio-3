namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Xunit;
using OpenXmlTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

public sealed class Sprint15SerialQuantityWordPipelineTests
{
    private static readonly IReadOnlyList<string> Headers =
        ["No", "Product Name", "Product No", "NSN", "Serial No", "Quantity"];

    [Fact]
    public void SerialQuantityPipeline_ExactTwoSerials_ComposerToWordWritesTrueVerticalMerge()
    {
        var tableElement = CreateConfiguredTable();
        var composition = new SerialQuantityTableContentRowComposer().Compose(
            tableElement,
            [
                ["1", "Elma", "1234", "321", "A222", "2"],
                ["2", "Elma", "1234", "321", "A221", "2"]
            ]);
        var node = new TableContentNode
        {
            ElementId = tableElement.Id,
            Kind = ReportContentKind.Table,
            Name = "Sprint 15 Pipeline Table",
            Caption = null,
            ColumnHeaders = Headers,
            Rows = composition.Rows,
            CellSpans = composition.CellSpans,
            RowGroups = composition.RowGroups,
            CompositionWarnings = composition.Warnings,
            DataSourceName = null,
            SourceCount = 0,
            SourceError = null,
            FilterWasIgnored = false
        };

        var table = WordTableWriter.BuildTable(node);
        var dataRows = GetDataRows(table);

        Assert.Equal(2, dataRows.Length);
        Assert.All(dataRows, row => Assert.Equal(6, row.Elements<TableCell>().Count()));
        foreach (var columnIndex in new[] { 0, 1, 2, 3, 5 })
        {
            Assert.Equal(MergedCellValues.Restart, GetMerge(table, 0, columnIndex)!.Val!.Value);
            Assert.Equal(MergedCellValues.Continue, GetMerge(table, 1, columnIndex)!.Val!.Value);
        }

        Assert.Null(GetMerge(table, 0, 4));
        Assert.Null(GetMerge(table, 1, 4));
        Assert.Equal("A222", GetDataCell(table, 0, 4).InnerText);
        Assert.Equal("A221", GetDataCell(table, 1, 4).InnerText);
    }

    private static TableElement CreateConfiguredTable()
    {
        var table = new TableElement { Name = "Sprint 15 Pipeline Table" };
        foreach (var header in Headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });

        table.SerialQuantityGrouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = table.Columns[2].Id,
            SerialNumberColumnId = table.Columns[4].Id,
            QuantityColumnId = table.Columns[5].Id,
            WasAutoDetected = false
        };
        return table;
    }

    private static OpenXmlTableRow[] GetDataRows(OpenXmlTable table) =>
        table.Elements<OpenXmlTableRow>().Skip(1).ToArray();

    private static TableCell GetDataCell(OpenXmlTable table, int rowIndex, int columnIndex) =>
        GetDataRows(table)[rowIndex].Elements<TableCell>().ElementAt(columnIndex);

    private static VerticalMerge? GetMerge(OpenXmlTable table, int rowIndex, int columnIndex) =>
        GetDataCell(table, rowIndex, columnIndex).TableCellProperties!.GetFirstChild<VerticalMerge>();
}
