namespace KKL.WordStudio.Infrastructure.Tests;

using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using Xunit;

public sealed class Sprint24PaginationParityWordTests
{
    [Fact]
    public void Caption_IsKeptWithFollowingTable()
    {
        var body = new Body();

        WordContentWriter.AppendNode(body, CreateTable(repeatHeader: true));

        var caption = Assert.Single(body.Elements<Paragraph>());
        Assert.NotNull(caption.ParagraphProperties?.GetFirstChild<KeepNext>());
        Assert.Single(body.Elements<Table>());
    }

    [Fact]
    public void EveryWordTableRow_IsAtomicAcrossPages()
    {
        var body = new Body();

        WordContentWriter.AppendNode(body, CreateTable(repeatHeader: true));

        var table = Assert.Single(body.Elements<Table>());
        Assert.All(table.Elements<TableRow>(), row =>
            Assert.NotNull(row.TableRowProperties?.GetFirstChild<CantSplit>()));
    }

    [Fact]
    public void RepeatingHeader_RemainsNativeWordHeaderRow()
    {
        var body = new Body();

        WordContentWriter.AppendNode(body, CreateTable(repeatHeader: true));

        var table = Assert.Single(body.Elements<Table>());
        var header = table.Elements<TableRow>().First();
        Assert.NotNull(header.TableRowProperties?.GetFirstChild<TableHeader>());
        Assert.Null(table.Elements<TableRow>().Skip(1).First().TableRowProperties?.GetFirstChild<TableHeader>());
    }

    private static TableContentNode CreateTable(bool repeatHeader) => new()
    {
        ElementId = Guid.NewGuid(),
        Kind = ReportContentKind.Table,
        Name = "Table 1",
        Caption = "Table 1",
        ColumnHeaders = ["No", "Part"],
        Rows =
        [
            ["1", "Part A"],
            ["2", "Part B"]
        ],
        SourceCount = 0,
        Format = new()
        {
            WidthPercent = 100d,
            FixedLayout = true,
            BorderSizePoints = 0.5d,
            CellMarginTopMillimeters = 0d,
            CellMarginBottomMillimeters = 0d,
            CellMarginLeftMillimeters = 0d,
            CellMarginRightMillimeters = 0d,
            PreferredRowHeightMillimeters = 0d,
            RepeatHeader = repeatHeader,
            Columns = []
        }
    };
}
