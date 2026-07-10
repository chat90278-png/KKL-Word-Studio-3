namespace KKL.WordStudio.Application.TableComposition;

using KKL.WordStudio.Domain.Elements;

public sealed class SerialQuantityGroupingDetector : ISerialQuantityGroupingDetector
{
    public SerialQuantityGrouping? Detect(IReadOnlyList<TableColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);

        var matchKeys = columns.Where(ColumnRoleAliasNormalizer.MatchesMatchKey).ToList();
        var serials = columns.Where(ColumnRoleAliasNormalizer.MatchesSerial).ToList();
        var quantities = columns.Where(ColumnRoleAliasNormalizer.MatchesQuantity).ToList();

        if (matchKeys.Count != 1 || serials.Count != 1 || quantities.Count != 1)
            return null;

        if (matchKeys[0].Id == serials[0].Id
            || matchKeys[0].Id == quantities[0].Id
            || serials[0].Id == quantities[0].Id)
        {
            return null;
        }

        return new SerialQuantityGrouping
        {
            MatchKeyColumnId = matchKeys[0].Id,
            SerialNumberColumnId = serials[0].Id,
            QuantityColumnId = quantities[0].Id,
            WasAutoDetected = true
        };
    }
}
