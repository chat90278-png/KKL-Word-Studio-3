namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using Xunit;

public sealed class Sprint24DashPlaceholderCompositionTests
{
    [Fact]
    public void MergeableField_DashPlaceholderIsBlankButTwoRealValuesStillConflict()
    {
        var keyColumn = new TableColumn { Header = "Parça Numarası" };
        var nsnColumn = new TableColumn { Header = "NSN" };
        var serialColumn = new TableColumn { Header = "Seri Numarası" };
        var quantityColumn = new TableColumn { Header = "Adet" };
        var table = new TableElement { Name = "Table 1" };
        table.Columns.AddRange([keyColumn, nsnColumn, serialColumn, quantityColumn]);
        table.SerialQuantityGrouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = keyColumn.Id,
            SerialNumberColumnId = serialColumn.Id,
            QuantityColumnId = quantityColumn.Id
        };

        var composer = new SerialQuantityTableContentRowComposer();
        IReadOnlyList<IReadOnlyList<string>> placeholderRows =
        [
            new[] { "2354", "554-415", "S-1", "2" },
            new[] { "2354", "-", "S-2", string.Empty }
        ];

        var placeholderResult = composer.Compose(table, placeholderRows);

        Assert.Empty(placeholderResult.Warnings);
        Assert.Equal(2, placeholderResult.Rows.Count);
        Assert.Equal("554-415", placeholderResult.Rows[0][1]);
        Assert.Equal(string.Empty, placeholderResult.Rows[1][1]);

        IReadOnlyList<IReadOnlyList<string>> conflictingRows =
        [
            new[] { "2354", "554-415", "S-1", "2" },
            new[] { "2354", "123-123", "S-2", string.Empty }
        ];

        var conflictResult = composer.Compose(table, conflictingRows);

        var warning = Assert.Single(conflictResult.Warnings);
        Assert.Contains("'NSN' alanında çelişkili değerler", warning, StringComparison.Ordinal);
    }
}
