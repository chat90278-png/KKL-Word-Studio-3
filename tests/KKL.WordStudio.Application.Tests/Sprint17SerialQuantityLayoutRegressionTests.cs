namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using Xunit;

public sealed class Sprint17SerialQuantityLayoutRegressionTests
{
    private readonly SerialQuantityGroupingDetector detector = new();
    private readonly SerialQuantityTableContentRowComposer composer = new();

    [Fact]
    public void PhysicalSerialRows_WithQuantityOneAndBlank_InferSerialCountAndCreateTrueGroupedLayout()
    {
        var table = CreateConfiguredTable(
            "No",
            "Tr İsim",
            "Parça Numarası",
            "NSN",
            "Seri Numarası",
            "Adet");

        var result = composer.Compose(table,
        [
            ["1", "elma", "1234", "45-50-60", "9999", "1"],
            ["2", "armut", "56789", "459-485-5", "9987", "1"],
            ["", "armut", "56789", "", "9988", ""]
        ]);

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(new[] { "1", "elma", "1234", "45-50-60", "9999", "1" }, result.Rows[0]);
        Assert.Equal(new[] { "2", "armut", "56789", "459-485-5", "9987", "2" }, result.Rows[1]);
        Assert.Equal(new[] { "", "", "", "", "9988", "" }, result.Rows[2]);

        AssertTwoSerialGroupedLayout(result, expectedStartRowIndex: 1);
    }

    [Fact]
    public void PhysicalSerialRows_WithQuantityOneOnBothRows_SumToSerialCountAndGroup()
    {
        var table = CreateConfiguredTable("Parça Numarası", "Seri Numarası", "Adet");

        var result = composer.Compose(table,
        [
            ["56789", "9987", "1"],
            ["56789", "9988", "1"]
        ]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(new[] { "56789", "9987", "2" }, result.Rows[0]);
        Assert.Equal(new[] { "", "9988", "" }, result.Rows[1]);
        AssertTwoSerialGroupedLayout(result, expectedStartRowIndex: 0);
    }

    [Fact]
    public void PhysicalSerialRows_WithExplicitTotalQuantity_UseExistingTotalAndGroup()
    {
        var table = CreateConfiguredTable("Parça Numarası", "Seri Numarası", "Adet");

        var result = composer.Compose(table,
        [
            ["56789", "9987", "2"],
            ["56789", "9988", ""]
        ]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(new[] { "56789", "9987", "2" }, result.Rows[0]);
        Assert.Equal(new[] { "", "9988", "" }, result.Rows[1]);
        AssertTwoSerialGroupedLayout(result, expectedStartRowIndex: 0);
    }

    [Fact]
    public void ExplicitQuantityThree_WithTwoPhysicalSerialRows_RemainsMismatch()
    {
        var table = CreateConfiguredTable("Parça Numarası", "Seri Numarası", "Adet");

        var result = composer.Compose(table,
        [
            ["56789", "9987", "3"],
            ["56789", "9988", ""]
        ]);

        var row = Assert.Single(result.Rows);
        Assert.Equal("9987\n9988", row[1]);
        Assert.Equal("3", row[2]);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Contains("Adet 3", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void QuantityOne_WithMultipleSerialTokensInOneCell_DoesNotPretendTheyWerePhysicalRows()
    {
        var table = CreateConfiguredTable("Parça Numarası", "Seri Numarası", "Adet");

        var result = composer.Compose(table,
        [
            ["56789", "9987\n9988", "1"]
        ]);

        var row = Assert.Single(result.Rows);
        Assert.Equal("9987\n9988", row[1]);
        Assert.Equal("1", row[2]);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Contains("Adet 1", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void QuantityOne_WithPackedAndPhysicalSerialCells_DoesNotInferAFalsePhysicalTotal()
    {
        var table = CreateConfiguredTable("Parça Numarası", "Seri Numarası", "Adet");

        var result = composer.Compose(table,
        [
            ["56789", "9987\n9988", "1"],
            ["56789", "9989", ""]
        ]);

        var row = Assert.Single(result.Rows);
        Assert.Equal("9987\n9988\n9989", row[1]);
        Assert.Equal("1", row[2]);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Contains("Adet 1", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    private static void AssertTwoSerialGroupedLayout(
        KKL.WordStudio.Application.Tables.TableRowCompositionResult result,
        int expectedStartRowIndex)
    {
        var group = Assert.Single(result.RowGroups);
        Assert.Equal(expectedStartRowIndex, group.StartRowIndex);
        Assert.Equal(2, group.RowCount);
        Assert.True(group.KeepTogetherWhenPossible);
        Assert.All(result.CellSpans, span => Assert.Equal(2, span.RowSpan));
        Assert.Empty(result.Warnings);
    }

    private TableElement CreateConfiguredTable(params string[] headers)
    {
        var table = new TableElement { Name = "Sprint17 grouped layout regression" };
        foreach (var header in headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });

        table.SerialQuantityGrouping = detector.Detect(table.Columns);
        Assert.NotNull(table.SerialQuantityGrouping);
        return table;
    }
}
