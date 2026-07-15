namespace KKL.WordStudio.Application.TableComposition;

using System.Globalization;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.Elements;

public sealed class SerialQuantityTableContentRowComposer : ITableContentRowComposer
{
    private const NumberStyles QuantityNumberStyles =
        NumberStyles.AllowLeadingWhite
        | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign
        | NumberStyles.AllowDecimalPoint;

    public TableRowCompositionResult Compose(
        TableElement table,
        IReadOnlyList<IReadOnlyList<string>> normalizedRows)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(normalizedRows);

        if (table.SerialQuantityGrouping is null)
            return PassThrough(normalizedRows);

        var grouping = table.SerialQuantityGrouping;
        var matchKeyIndex = FindColumnIndex(table.Columns, grouping.MatchKeyColumnId);
        var serialIndex = FindColumnIndex(table.Columns, grouping.SerialNumberColumnId);
        var quantityIndex = FindColumnIndex(table.Columns, grouping.QuantityColumnId);
        if (matchKeyIndex < 0 || serialIndex < 0 || quantityIndex < 0)
        {
            return new TableRowCompositionResult
            {
                Rows = normalizedRows,
                CellSpans = [],
                RowGroups = [],
                Warnings = ["Seri no/adet düzeni yapılandırması geçersiz; tablo satırları değiştirilmedi."]
            };
        }

        var rows = NormalizeRows(normalizedRows, table.Columns.Count);
        var groupedRows = BuildGroups(rows, matchKeyIndex);
        var emittedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outputRows = new List<IReadOnlyList<string>>();
        var spans = new List<TableCellSpan>();
        var rowGroups = new List<TableRowGroup>();
        var warnings = new List<string>();
        var ordinalColumns = ResolveOrdinalColumns(table.Columns, matchKeyIndex, serialIndex, quantityIndex);

        foreach (var row in rows)
        {
            var key = row[matchKeyIndex].Trim();
            if (key.Length == 0)
            {
                outputRows.Add(row);
                continue;
            }

            if (!emittedKeys.Add(key))
                continue;

            var groupRows = groupedRows[key];
            ComposeGroup(
                table,
                key,
                groupRows,
                matchKeyIndex,
                serialIndex,
                quantityIndex,
                ordinalColumns,
                outputRows,
                spans,
                rowGroups,
                warnings);
        }

        return new TableRowCompositionResult
        {
            Rows = outputRows,
            CellSpans = spans,
            RowGroups = rowGroups,
            Warnings = warnings
        };
    }

    private static void ComposeGroup(
        TableElement table,
        string key,
        IReadOnlyList<string[]> groupRows,
        int matchKeyIndex,
        int serialIndex,
        int quantityIndex,
        HashSet<int> ordinalColumns,
        List<IReadOnlyList<string>> outputRows,
        List<TableCellSpan> spans,
        List<TableRowGroup> rowGroups,
        List<string> warnings)
    {
        var observedSerials = ReadObservedSerialTokens(groupRows, serialIndex);
        var distinctSerialCount = observedSerials.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var hasDuplicates = distinctSerialCount != observedSerials.Count;
        var quantity = ResolveQuantity(groupRows, quantityIndex);
        quantity = InferQuantityFromPhysicalSerialRows(
            quantity,
            groupRows,
            serialIndex,
            observedSerials.Count,
            hasDuplicates);

        var groupWarnings = new List<string>();
        if (!quantity.IsSafe)
            groupWarnings.Add(BuildQuantityWarning(key, quantity));

        var canonical = new string[table.Columns.Count];
        for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
        {
            if (columnIndex == serialIndex)
                continue;

            if (columnIndex == quantityIndex)
            {
                canonical[columnIndex] = quantity.IsSafe
                    ? quantity.CanonicalText
                    : FirstNonBlank(groupRows, columnIndex);
                continue;
            }

            if (columnIndex == matchKeyIndex || ordinalColumns.Contains(columnIndex))
            {
                canonical[columnIndex] = FirstNonBlank(groupRows, columnIndex);
                continue;
            }

            var values = DistinctNonBlankValues(groupRows, columnIndex);
            if (values.Count > 1)
            {
                groupWarnings.Add($"PN/key '{key}' için '{table.Columns[columnIndex].Header}' alanında çelişkili değerler var; satırlar birleştirilmedi.");
            }
            else
            {
                canonical[columnIndex] = values.Count == 1 ? values[0] : string.Empty;
            }
        }

        if (groupWarnings.Count > 0)
        {
            outputRows.AddRange(groupRows);
            warnings.AddRange(groupWarnings);
            return;
        }

        if (quantity.Value > 1
            && observedSerials.Count == quantity.Value
            && !hasDuplicates)
        {
            var startRowIndex = outputRows.Count;
            for (var serialOffset = 0; serialOffset < observedSerials.Count; serialOffset++)
            {
                var output = new string[table.Columns.Count];
                Array.Fill(output, string.Empty);
                if (serialOffset == 0)
                    Array.Copy(canonical, output, canonical.Length);
                output[serialIndex] = observedSerials[serialOffset];
                outputRows.Add(output);
            }

            for (var columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
            {
                if (columnIndex == serialIndex)
                    continue;

                spans.Add(new TableCellSpan
                {
                    RowIndex = startRowIndex,
                    ColumnIndex = columnIndex,
                    RowSpan = quantity.Value
                });
            }

            rowGroups.Add(new TableRowGroup
            {
                StartRowIndex = startRowIndex,
                RowCount = quantity.Value,
                KeepTogetherWhenPossible = true
            });
            return;
        }

        var aggregate = canonical;
        aggregate[serialIndex] = string.Join('\n', observedSerials);
        outputRows.Add(aggregate);

        if (observedSerials.Count <= 1)
            return;

        if (hasDuplicates)
        {
            warnings.Add($"PN/key '{key}' için tekrarlanan Seri No değerleri var; çoklu seri düzeni uygulanmadı.");
            return;
        }

        warnings.Add($"PN/key '{key}' için Adet {quantity.Value}, eşleşen Seri No {observedSerials.Count}; çoklu seri düzeni uygulanmadı.");
    }

    private static QuantityResolution InferQuantityFromPhysicalSerialRows(
        QuantityResolution quantity,
        IReadOnlyList<string[]> rows,
        int serialIndex,
        int observedSerialCount,
        bool hasDuplicates)
    {
        if (!quantity.IsSafe
            || quantity.Value != 1
            || observedSerialCount <= 1
            || hasDuplicates)
        {
            return quantity;
        }

        var physicalSerialRowCount = rows.Count(row => !string.IsNullOrWhiteSpace(row[serialIndex]));
        if (physicalSerialRowCount <= 1 || physicalSerialRowCount != observedSerialCount)
            return quantity;

        return QuantityResolution.Safe(
            observedSerialCount,
            observedSerialCount.ToString(CultureInfo.InvariantCulture));
    }

    private static QuantityResolution ResolveQuantity(IReadOnlyList<string[]> rows, int quantityIndex)
    {
        var parsedValues = new HashSet<int>();
        string? canonicalText = null;
        var malformed = false;

        foreach (var row in rows)
        {
            var raw = row[quantityIndex];
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            canonicalText ??= raw;
            if (!TryParsePositiveWholeNumber(raw, out var parsed))
            {
                malformed = true;
                continue;
            }

            parsedValues.Add(parsed);
        }

        if (malformed)
            return QuantityResolution.Malformed(canonicalText ?? string.Empty);
        if (parsedValues.Count > 1)
            return QuantityResolution.Conflicting(canonicalText ?? string.Empty);
        if (parsedValues.Count == 0)
            return QuantityResolution.Missing();

        return QuantityResolution.Safe(parsedValues.Single(), canonicalText ?? string.Empty);
    }

    private static bool TryParsePositiveWholeNumber(string text, out int value)
    {
        foreach (var culture in QuantityCultures())
        {
            if (!decimal.TryParse(text, QuantityNumberStyles, culture, out var parsed)
                || parsed <= 0
                || parsed != decimal.Truncate(parsed)
                || parsed > int.MaxValue)
            {
                continue;
            }

            value = decimal.ToInt32(parsed);
            return true;
        }

        value = 0;
        return false;
    }

    private static IEnumerable<CultureInfo> QuantityCultures()
    {
        yield return CultureInfo.CurrentCulture;
        if (!Equals(CultureInfo.CurrentCulture, CultureInfo.InvariantCulture))
            yield return CultureInfo.InvariantCulture;
    }

    private static string BuildQuantityWarning(string key, QuantityResolution quantity) => quantity.FailureKind switch
    {
        QuantityFailureKind.Malformed => $"PN/key '{key}' için geçersiz Adet değeri var; satırlar birleştirilmedi.",
        QuantityFailureKind.Conflicting => $"PN/key '{key}' için çelişkili Adet değerleri var; satırlar birleştirilmedi.",
        _ => $"PN/key '{key}' için geçerli Adet değeri bulunamadı; satırlar birleştirilmedi."
    };

    private static List<string> ReadObservedSerialTokens(IReadOnlyList<string[]> rows, int serialIndex)
    {
        var tokens = new List<string>();
        foreach (var row in rows)
        {
            var serialCell = row[serialIndex];
            foreach (var token in serialCell.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                var trimmed = token.Trim();
                if (trimmed.Length > 0)
                    tokens.Add(trimmed);
            }
        }

        return tokens;
    }

    private static List<string> DistinctNonBlankValues(IReadOnlyList<string[]> rows, int columnIndex)
    {
        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var value = row[columnIndex].Trim();
            if (!IsBlankMergePlaceholder(value) && seen.Add(value))
                values.Add(value);
        }

        return values;
    }

    private static bool IsBlankMergePlaceholder(string value) =>
        value.Length == 0 || value is "-" or "–" or "—" or "‑" or "‒";

    private static string FirstNonBlank(IReadOnlyList<string[]> rows, int columnIndex) =>
        rows.Select(row => row[columnIndex].Trim()).FirstOrDefault(value => value.Length > 0) ?? string.Empty;

    private static Dictionary<string, List<string[]>> BuildGroups(IReadOnlyList<string[]> rows, int matchKeyIndex)
    {
        var groups = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var key = row[matchKeyIndex].Trim();
            if (key.Length == 0)
                continue;

            if (!groups.TryGetValue(key, out var groupRows))
            {
                groupRows = [];
                groups.Add(key, groupRows);
            }
            groupRows.Add(row);
        }

        return groups;
    }

    private static List<string[]> NormalizeRows(
        IReadOnlyList<IReadOnlyList<string>> rows,
        int columnCount)
    {
        var normalized = new List<string[]>(rows.Count);
        foreach (var row in rows)
        {
            var cells = new string[columnCount];
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
                cells[columnIndex] = columnIndex < row.Count ? row[columnIndex] ?? string.Empty : string.Empty;
            normalized.Add(cells);
        }
        return normalized;
    }

    private static HashSet<int> ResolveOrdinalColumns(
        IReadOnlyList<TableColumn> columns,
        int matchKeyIndex,
        int serialIndex,
        int quantityIndex)
    {
        var indexes = new HashSet<int>();
        for (var index = 0; index < columns.Count; index++)
        {
            if (index != matchKeyIndex
                && index != serialIndex
                && index != quantityIndex
                && ColumnRoleAliasNormalizer.MatchesOrdinal(columns[index]))
            {
                indexes.Add(index);
            }
        }
        return indexes;
    }

    private static int FindColumnIndex(IReadOnlyList<TableColumn> columns, Guid id)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (columns[index].Id == id)
                return index;
        }
        return -1;
    }

    private static TableRowCompositionResult PassThrough(IReadOnlyList<IReadOnlyList<string>> rows) => new()
    {
        Rows = rows,
        CellSpans = [],
        RowGroups = [],
        Warnings = []
    };

    private sealed record QuantityResolution(
        bool IsSafe,
        int Value,
        string CanonicalText,
        QuantityFailureKind FailureKind)
    {
        public static QuantityResolution Safe(int value, string canonicalText) =>
            new(true, value, canonicalText, QuantityFailureKind.None);

        public static QuantityResolution Missing() =>
            new(false, 0, string.Empty, QuantityFailureKind.Missing);

        public static QuantityResolution Malformed(string canonicalText) =>
            new(false, 0, canonicalText, QuantityFailureKind.Malformed);

        public static QuantityResolution Conflicting(string canonicalText) =>
            new(false, 0, canonicalText, QuantityFailureKind.Conflicting);
    }

    private enum QuantityFailureKind
    {
        None,
        Missing,
        Malformed,
        Conflicting
    }
}
