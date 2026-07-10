namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Xunit;
using OpenXmlTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OpenXmlTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

public sealed class Sprint15WordTableMergeTests
{
    [Fact]
    public void WordTable_VerticalSpanAnchor_WritesRestart()
    {
        var table = WordTableWriter.BuildTable(CreateGroupedNode());

        var merge = GetDataCell(table, 0, 0).TableCellProperties!.GetFirstChild<VerticalMerge>();

        Assert.NotNull(merge);
        Assert.Equal(MergedCellValues.Restart, merge.Val!.Value);
    }

    [Fact]
    public void WordTable_VerticalSpanContinuation_WritesContinue()
    {
        var table = WordTableWriter.BuildTable(CreateGroupedNode());

        var cell = GetDataCell(table, 1, 0);
        var merge = cell.TableCellProperties!.GetFirstChild<VerticalMerge>();

        Assert.NotNull(merge);
        Assert.Equal(MergedCellValues.Continue, merge.Val!.Value);
        Assert.Equal(string.Empty, cell.InnerText);
    }

    [Fact]
    public void WordTable_SerialColumn_RemainsUnmerged()
    {
        var table = WordTableWriter.BuildTable(CreateGroupedNode());

        Assert.Null(GetDataCell(table, 0, 4).TableCellProperties!.GetFirstChild<VerticalMerge>());
        Assert.Null(GetDataCell(table, 1, 4).TableCellProperties!.GetFirstChild<VerticalMerge>());
        Assert.Equal("A222", GetDataCell(table, 0, 4).InnerText);
        Assert.Equal("A221", GetDataCell(table, 1, 4).InnerText);
    }

    [Fact]
    public void WordTable_MergedCells_AreVerticallyCentered()
    {
        var table = WordTableWriter.BuildTable(CreateGroupedNode());

        foreach (var dataRowIndex in new[] { 0, 1 })
        {
            foreach (var columnIndex in new[] { 0, 1, 2, 3, 5 })
            {
                var alignment = GetDataCell(table, dataRowIndex, columnIndex)
                    .TableCellProperties!
                    .GetFirstChild<TableCellVerticalAlignment>();
                Assert.NotNull(alignment);
                Assert.Equal(TableVerticalAlignmentValues.Center, alignment.Val!.Value);
            }
        }

        Assert.Null(GetDataCell(table, 0, 4).TableCellProperties!.GetFirstChild<TableCellVerticalAlignment>());
    }

    [Fact]
    public void WordTable_HeaderRowOffset_DoesNotShiftSemanticSpanIndexes()
    {
        var table = WordTableWriter.BuildTable(CreateGroupedNode());
        var rows = table.Elements<OpenXmlTableRow>().ToArray();

        Assert.NotNull(rows[0].TableRowProperties?.GetFirstChild<TableHeader>());
        Assert.Null(rows[0].Elements<TableCell>().First().TableCellProperties!.GetFirstChild<VerticalMerge>());
        Assert.Equal(MergedCellValues.Restart, rows[1].Elements<TableCell>().First()
            .TableCellProperties!.GetFirstChild<VerticalMerge>()!.Val!.Value);
        Assert.Equal(MergedCellValues.Continue, rows[2].Elements<TableCell>().First()
            .TableCellProperties!.GetFirstChild<VerticalMerge>()!.Val!.Value);
    }

    [Fact]
    public void WordTable_EveryDataRowRetainsFullCellCount()
    {
        var node = CreateNode(
            headers: new[] { "A", "B", "C" },
            rows: new IReadOnlyList<string>[]
            {
                new[] { "1", "2", "3" },
                new[] { "4" }
            });

        var table = WordTableWriter.BuildTable(node);
        var dataRows = GetDataRows(table);

        Assert.All(dataRows, row => Assert.Equal(3, row.Elements<TableCell>().Count()));
        Assert.Equal(string.Empty, dataRows[1].Elements<TableCell>().ElementAt(1).InnerText);
        Assert.Equal(string.Empty, dataRows[1].Elements<TableCell>().ElementAt(2).InnerText);
    }

    [Fact]
    public void WordTable_MultipleIndependentColumnSpans_AreWritten()
    {
        var table = WordTableWriter.BuildTable(CreateGroupedNode());

        foreach (var columnIndex in new[] { 0, 1, 2, 3, 5 })
        {
            Assert.Equal(MergedCellValues.Restart, GetMerge(table, 0, columnIndex)!.Val!.Value);
            Assert.Equal(MergedCellValues.Continue, GetMerge(table, 1, columnIndex)!.Val!.Value);
        }
    }

    [Fact]
    public void WordTable_InvalidSpan_IsIgnoredWithoutCorruptingTable()
    {
        var node = CreateNode(
            headers: new[] { "A", "B" },
            rows: new IReadOnlyList<string>[]
            {
                new[] { "1", "2" },
                new[] { "3", "4" }
            },
            spans: new[]
            {
                Span(-1, 0, 2),
                Span(0, -1, 2),
                Span(0, 0, 1),
                Span(0, 2, 2),
                Span(1, 0, 2)
            });

        var table = RoundTripTable(node);

        Assert.Empty(table.Descendants<VerticalMerge>());
        Assert.Equal(2, GetDataRows(table).Length);
        Assert.All(GetDataRows(table), row => Assert.Equal(2, row.Elements<TableCell>().Count()));
    }

    [Fact]
    public void WordTable_OverlappingSpanMetadata_DoesNotEmitConflictingMergeState()
    {
        var node = CreateNode(
            headers: new[] { "A", "B" },
            rows: new IReadOnlyList<string>[]
            {
                new[] { "1", "a" },
                new[] { string.Empty, "b" },
                new[] { string.Empty, "c" }
            },
            spans: new[]
            {
                Span(0, 0, 3),
                Span(1, 0, 2)
            });

        var table = WordTableWriter.BuildTable(node);

        Assert.Empty(table.Descendants<VerticalMerge>());
        Assert.Equal("1", GetDataCell(table, 0, 0).InnerText);
    }

    [Fact]
    public void WordTable_GroupedExample_ReopensWithExpectedVMergeXml()
    {
        var table = RoundTripTable(CreateGroupedNode());
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
        Assert.Contains("w:vMerge w:val=\"restart\"", table.OuterXml);
        Assert.Contains("w:vMerge w:val=\"continue\"", table.OuterXml);
    }

    [Fact]
    public void WordTable_ExistingRepeatedHeaderBehavior_IsPreserved()
    {
        var table = WordTableWriter.BuildTable(CreateGroupedNode());

        var headerRow = table.Elements<OpenXmlTableRow>().First();

        Assert.NotNull(headerRow.TableRowProperties?.GetFirstChild<TableHeader>());
    }

    [Fact]
    public void WordTable_ExistingWidthAndFixedLayoutBehavior_IsPreserved()
    {
        var table = WordTableWriter.BuildTable(CreateGroupedNode());
        var properties = table.GetFirstChild<TableProperties>()!;
        var tableWidth = properties.GetFirstChild<TableWidth>()!;
        var layout = properties.GetFirstChild<TableLayout>()!;
        var grid = table.GetFirstChild<TableGrid>()!;
        var firstRowWidths = table.Elements<OpenXmlTableRow>().First().Elements<TableCell>()
            .Select(cell => cell.TableCellProperties!.GetFirstChild<TableCellWidth>()!.Width!.Value)
            .ToArray();

        Assert.Equal(TableWidthUnitValues.Pct, tableWidth.Type!.Value);
        Assert.Equal("5000", tableWidth.Width!.Value);
        Assert.Equal(TableLayoutValues.Fixed, layout.Type!.Value);
        Assert.Equal(6, grid.Elements<GridColumn>().Count());
        Assert.Equal(6, firstRowWidths.Length);
        Assert.All(firstRowWidths, width => Assert.Equal("833", width));
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

    private static TableContentNode CreateGroupedNode() => CreateNode(
        headers: new[] { "No", "Product Name", "Product No", "NSN", "Serial No", "Quantity" },
        rows: new IReadOnlyList<string>[]
        {
            new[] { "1", "Elma", "1234", "321", "A222", "2" },
            new[] { string.Empty, string.Empty, string.Empty, string.Empty, "A221", string.Empty }
        },
        spans: new[]
        {
            Span(0, 0, 2),
            Span(0, 1, 2),
            Span(0, 2, 2),
            Span(0, 3, 2),
            Span(0, 5, 2)
        });

    private static TableContentNode CreateNode(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<TableCellSpan>? spans = null) => new()
    {
        ElementId = Guid.NewGuid(),
        Kind = ReportContentKind.Table,
        Name = "Sprint 15 Tablo",
        Caption = "Tablo 1",
        ColumnHeaders = headers,
        Rows = rows,
        CellSpans = spans ?? Array.Empty<TableCellSpan>(),
        DataSourceName = null,
        SourceCount = 0,
        SourceError = null,
        FilterWasIgnored = false
    };

    private static TableCellSpan Span(int rowIndex, int columnIndex, int rowSpan) => new()
    {
        RowIndex = rowIndex,
        ColumnIndex = columnIndex,
        RowSpan = rowSpan
    };

    private static OpenXmlTableRow[] GetDataRows(OpenXmlTable table) =>
        table.Elements<OpenXmlTableRow>().Skip(1).ToArray();

    private static TableCell GetDataCell(OpenXmlTable table, int rowIndex, int columnIndex) =>
        GetDataRows(table)[rowIndex].Elements<TableCell>().ElementAt(columnIndex);

    private static VerticalMerge? GetMerge(OpenXmlTable table, int rowIndex, int columnIndex) =>
        GetDataCell(table, rowIndex, columnIndex).TableCellProperties!.GetFirstChild<VerticalMerge>();
}
