namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Domain.Elements;

/// <summary>
/// Applies a small deterministic width correction to automatic built-in table
/// profiles when a long header cannot fit the profile's assigned column share.
/// Explicit selections and imported reference profiles remain exact.
/// </summary>
public static class AutomaticTableFormatWidthAdapter
{
    private const double MinimumSharePercent = 4d;
    private const double MaximumMinimumSharePercent = 22d;
    private const double SharePerLongestTokenCharacter = 1.6d;

    public static ResolvedTableFormat Adapt(
        ReferenceTableFormatProfile selectedProfile,
        TableElement table)
    {
        ArgumentNullException.ThrowIfNull(selectedProfile);
        ArgumentNullException.ThrowIfNull(table);

        var format = selectedProfile.Format;
        if (!string.IsNullOrWhiteSpace(table.ReferenceTableFormatKey)
            || !selectedProfile.Key.StartsWith("built-in-", StringComparison.Ordinal)
            || format.Columns.Count != table.Columns.Count
            || format.Columns.Count == 0)
        {
            return format;
        }

        var baseWeights = format.Columns
            .Select(column => Math.Max(0.0001d, column.WidthWeight))
            .ToArray();
        var totalWeight = baseWeights.Sum();
        var minimumWeights = table.Columns
            .Select(column => RequiredMinimumWeight(column.Header, totalWeight))
            .ToArray();

        var requestedIncreases = baseWeights
            .Select((weight, index) => Math.Max(0d, minimumWeights[index] - weight))
            .ToArray();
        var donorCapacities = baseWeights
            .Select((weight, index) => Math.Max(0d, weight - minimumWeights[index]))
            .ToArray();
        var requestedTotal = requestedIncreases.Sum();
        var donorTotal = donorCapacities.Sum();
        var transferable = Math.Min(requestedTotal, donorTotal);

        if (transferable <= 0.0001d)
            return format;

        var increaseScale = transferable / requestedTotal;
        var donorScale = transferable / donorTotal;
        var adjustedWeights = baseWeights
            .Select((weight, index) =>
                weight
                + requestedIncreases[index] * increaseScale
                - donorCapacities[index] * donorScale)
            .ToArray();

        if (!adjustedWeights.Where((weight, index) => Math.Abs(weight - baseWeights[index]) > 0.0001d).Any())
            return format;

        return new ResolvedTableFormat
        {
            WidthPercent = format.WidthPercent,
            FixedLayout = format.FixedLayout,
            BorderSizePoints = format.BorderSizePoints,
            CellMarginTopMillimeters = format.CellMarginTopMillimeters,
            CellMarginBottomMillimeters = format.CellMarginBottomMillimeters,
            CellMarginLeftMillimeters = format.CellMarginLeftMillimeters,
            CellMarginRightMillimeters = format.CellMarginRightMillimeters,
            PreferredRowHeightMillimeters = format.PreferredRowHeightMillimeters,
            RepeatHeader = format.RepeatHeader,
            Columns = format.Columns
                .Select((column, index) => CloneColumn(column, adjustedWeights[index]))
                .ToArray()
        };
    }

    private static double RequiredMinimumWeight(string? header, double totalWeight)
    {
        var longestTokenLength = LongestTokenLength(header);
        var minimumSharePercent = Math.Clamp(
            longestTokenLength * SharePerLongestTokenCharacter,
            MinimumSharePercent,
            MaximumMinimumSharePercent);
        return totalWeight * minimumSharePercent / 100d;
    }

    private static int LongestTokenLength(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var longest = 0;
        var current = 0;
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                current++;
                longest = Math.Max(longest, current);
            }
            else
            {
                current = 0;
            }
        }

        return longest;
    }

    private static ResolvedTableColumnFormat CloneColumn(
        ResolvedTableColumnFormat source,
        double widthWeight) => new()
    {
        WidthWeight = widthWeight,
        HeaderAlignment = source.HeaderAlignment,
        BodyAlignment = source.BodyAlignment,
        HeaderFontFamilyName = source.HeaderFontFamilyName,
        HeaderFontSizePoints = source.HeaderFontSizePoints,
        HeaderBold = source.HeaderBold,
        BodyFontFamilyName = source.BodyFontFamilyName,
        BodyFontSizePoints = source.BodyFontSizePoints,
        BodyBold = source.BodyBold,
        VerticalAlignment = source.VerticalAlignment,
        NoWrap = source.NoWrap
    };
}
