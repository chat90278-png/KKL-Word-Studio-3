namespace KKL.WordStudio.Domain.Tests;

using KKL.WordStudio.Domain.Elements;
using Xunit;

public sealed class Sprint15ContractBootstrapTests
{
    [Fact]
    public void TableElement_HasPersistedSerialQuantityGroupingIdentity()
    {
        var grouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = Guid.NewGuid(),
            SerialNumberColumnId = Guid.NewGuid(),
            QuantityColumnId = Guid.NewGuid(),
            WasAutoDetected = true
        };
        var table = new TableElement { SerialQuantityGrouping = grouping };

        Assert.Same(grouping, table.SerialQuantityGrouping);
        Assert.True(table.SerialQuantityGrouping.WasAutoDetected);
    }

    [Fact]
    public void SerialQuantityGrouping_UsesStableColumnIds()
    {
        var properties = typeof(SerialQuantityGrouping).GetProperties()
            .ToDictionary(property => property.Name, property => property.PropertyType);

        Assert.Equal(typeof(Guid), properties[nameof(SerialQuantityGrouping.MatchKeyColumnId)]);
        Assert.Equal(typeof(Guid), properties[nameof(SerialQuantityGrouping.SerialNumberColumnId)]);
        Assert.Equal(typeof(Guid), properties[nameof(SerialQuantityGrouping.QuantityColumnId)]);
        Assert.Equal(typeof(bool), properties[nameof(SerialQuantityGrouping.WasAutoDetected)]);
        Assert.Equal(4, properties.Count);
    }
}
