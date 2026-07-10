namespace KKL.WordStudio.Architecture.Tests;

using System.Reflection;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.Elements;
using Xunit;

public sealed class Sprint15FrozenContractGuardTests
{
    [Fact]
    public void DomainGroupingContract_UsesStableGuidColumnIdentities()
    {
        var groupingProperty = typeof(TableElement).GetProperty(nameof(TableElement.SerialQuantityGrouping));
        Assert.NotNull(groupingProperty);
        Assert.Equal(typeof(SerialQuantityGrouping), groupingProperty!.PropertyType);

        AssertExactPublicProperties<SerialQuantityGrouping>(
            (nameof(SerialQuantityGrouping.MatchKeyColumnId), typeof(Guid)),
            (nameof(SerialQuantityGrouping.SerialNumberColumnId), typeof(Guid)),
            (nameof(SerialQuantityGrouping.QuantityColumnId), typeof(Guid)),
            (nameof(SerialQuantityGrouping.WasAutoDetected), typeof(bool)));
    }

    [Fact]
    public void ApplicationTableContracts_MatchFrozenSprint15Shape()
    {
        AssertExactPublicProperties<TableCellSpan>(
            (nameof(TableCellSpan.RowIndex), typeof(int)),
            (nameof(TableCellSpan.ColumnIndex), typeof(int)),
            (nameof(TableCellSpan.RowSpan), typeof(int)));
        AssertExactPublicProperties<TableRowGroup>(
            (nameof(TableRowGroup.StartRowIndex), typeof(int)),
            (nameof(TableRowGroup.RowCount), typeof(int)),
            (nameof(TableRowGroup.KeepTogetherWhenPossible), typeof(bool)));
        AssertExactPublicProperties<TableRowCompositionResult>(
            (nameof(TableRowCompositionResult.Rows), typeof(IReadOnlyList<IReadOnlyList<string>>)),
            (nameof(TableRowCompositionResult.CellSpans), typeof(IReadOnlyList<TableCellSpan>)),
            (nameof(TableRowCompositionResult.RowGroups), typeof(IReadOnlyList<TableRowGroup>)),
            (nameof(TableRowCompositionResult.Warnings), typeof(IReadOnlyList<string>)));

        Assert.True(typeof(ITableContentRowComposer).IsInterface, "ITableContentRowComposer must remain an interface.");
        var methods = typeof(ITableContentRowComposer).GetMethods();
        var compose = Assert.Single(methods);
        Assert.Equal(nameof(ITableContentRowComposer.Compose), compose.Name);
        Assert.Equal(typeof(TableRowCompositionResult), compose.ReturnType);
        Assert.Equal(
            new[] { typeof(TableElement), typeof(IReadOnlyList<IReadOnlyList<string>>) },
            compose.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    [Fact]
    public void TableContentNode_RetainsRowsAndSprint15SemanticCollections()
    {
        AssertProperty<TableContentNode>(nameof(TableContentNode.Rows), typeof(IReadOnlyList<IReadOnlyList<string>>));
        AssertProperty<TableContentNode>(nameof(TableContentNode.CellSpans), typeof(IReadOnlyList<TableCellSpan>));
        AssertProperty<TableContentNode>(nameof(TableContentNode.RowGroups), typeof(IReadOnlyList<TableRowGroup>));
        AssertProperty<TableContentNode>(nameof(TableContentNode.CompositionWarnings), typeof(IReadOnlyList<string>));
    }

    [Fact]
    public void TablePagePayload_ExposesFragmentLocalCellSpans()
    {
        AssertProperty<TablePageBlockPayload>(nameof(TablePageBlockPayload.Rows), typeof(IReadOnlyList<IReadOnlyList<string>>));
        AssertProperty<TablePageBlockPayload>(nameof(TablePageBlockPayload.CellSpans), typeof(IReadOnlyList<TableCellSpan>));
    }

    private static void AssertExactPublicProperties<T>(params (string Name, Type Type)[] expected)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var expectedByName = expected.ToDictionary(item => item.Name, item => item.Type, StringComparer.Ordinal);
        var actualNames = properties.Select(property => property.Name).ToHashSet(StringComparer.Ordinal);

        Assert.True(
            expectedByName.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(actualNames),
            $"Frozen Sprint 15 contract drift on {typeof(T).FullName}. Expected [{string.Join(", ", expectedByName.Keys.OrderBy(name => name, StringComparer.Ordinal))}], actual [{string.Join(", ", actualNames.OrderBy(name => name, StringComparer.Ordinal))}].");

        foreach (var property in properties)
        {
            Assert.Equal(expectedByName[property.Name], property.PropertyType);
            Assert.True(property.CanRead && property.CanWrite, $"{typeof(T).Name}.{property.Name} must remain publicly readable and settable/init-only.");
        }
    }

    private static PropertyInfo AssertProperty<T>(string propertyName, Type propertyType)
    {
        var property = typeof(T).GetProperty(propertyName);
        Assert.True(property is not null, $"Frozen Sprint 15 contract property missing: {typeof(T).FullName}.{propertyName}.");
        Assert.Equal(propertyType, property!.PropertyType);
        return property;
    }
}
