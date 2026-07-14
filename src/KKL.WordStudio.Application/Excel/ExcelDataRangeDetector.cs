namespace KKL.WordStudio.Application.Excel;

using System.Globalization;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Deterministic range heuristic. It combines worksheet-shape evidence with
/// explicit Turkish/English semantic header aliases. Semantic matches strongly
/// influence header selection but unknown columns remain untouched and available
/// for manual configuration.
/// </summary>
public sealed class ExcelDataRangeDetector : IExcelDataRangeDetector
{
    private const double StrongOverlap = 0.60;
    private const double SemanticHeaderOverlap = 0.35;
    private static readonly IExcelSemanticFieldMatcher SemanticMatcher = new ExcelSemanticFieldMatcher();

    public Result<ExcelDataRangeCandidate> Detect(SheetPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var rows = preview.Rows
            .Select((cells, index) => AnalyzeRow(preview.RowNumbers[index], cells))
            .Where(row => row.OccupiedColumns.Count > 0)
            .ToList();

        if (rows.Count == 0)
            return Result.Failure<ExcelDataRangeCandidate>("Sayfada algılanabilir veri bulunamadı.");

        var startIndex = FindBestStart(rows);
        var current = rows[startIndex];
        var next = startIndex + 1 < rows.Count ? rows[startIndex + 1] : null;
        var overlap = next is null ? 0 : ColumnOverlap(current, next);
        var semanticHeader = next is not null
            && AreAdjacent(current, next)
            && current.SemanticMatches.Count >= 2
            && overlap >= SemanticHeaderOverlap;
        var hasHeader = next is not null
            && AreAdjacent(current, next)
            && ((overlap >= StrongOverlap && IsProbableHeader(current, next)) || semanticHeader);

        var dataStartIndex = hasHeader ? startIndex + 1 : startIndex;
        var dataStart = rows[dataStartIndex];
        var block = BuildPreviewBlock(rows, dataStartIndex);
        var (startColumn, endColumn) = ResolveActiveColumns(block, hasHeader ? current : null);

        var strongContinuation = block.Count >= 2
            && block.Zip(block.Skip(1), (left, right) => AreAdjacent(left, right) && ColumnOverlap(left, right) >= StrongOverlap)
                .Any(value => value);
        var hasUnambiguousDataShape = block.Any(row => row.ScalarRatio > 0 || !row.IsLabelLike);
        var semanticConfidence = hasHeader
            && (current.SemanticMatches.Count >= 3
                || (current.SemanticMatches.Count >= 2 && strongContinuation));
        var confidence = semanticConfidence
            || (hasHeader && overlap >= 0.75)
            || (strongContinuation && hasUnambiguousDataShape)
                ? ExcelDataRangeConfidence.High
                : ExcelDataRangeConfidence.Low;

        return Result.Success(new ExcelDataRangeCandidate
        {
            HeaderRowIndex = hasHeader ? current.RowNumber : null,
            DataStartRow = dataStart.RowNumber,
            StartColumn = startColumn,
            EndColumn = endColumn,
            Confidence = confidence,
            SemanticFields = hasHeader
                ? current.SemanticMatches
                    .Where(match => match.ColumnIndex >= startColumn && match.ColumnIndex <= endColumn)
                    .ToList()
                : Array.Empty<ExcelSemanticFieldMatch>()
        });
    }

    private static int FindBestStart(IReadOnlyList<RowProfile> rows)
    {
        if (rows.Count == 1)
            return 0;

        var bestIndex = 0;
        var bestScore = double.MinValue;

        for (var index = 0; index < rows.Count - 1; index++)
        {
            var current = rows[index];
            var next = rows[index + 1];
            if (!AreAdjacent(current, next))
                continue;

            var overlap = ColumnOverlap(current, next);
            var hasSemanticShape = current.SemanticMatches.Count >= 2 && overlap >= SemanticHeaderOverlap;
            if (overlap < StrongOverlap && !hasSemanticShape)
                continue;

            var density = Math.Min(current.OccupiedColumns.Count, next.OccupiedColumns.Count);
            var continuation = index + 2 < rows.Count
                && AreAdjacent(next, rows[index + 2])
                && ColumnOverlap(next, rows[index + 2]) >= StrongOverlap;
            var distinctSemanticRoles = current.SemanticMatches
                .Select(match => match.Role)
                .Distinct()
                .Count();
            var score = (density * 2.0)
                + (overlap * 2.0)
                + (IsProbableHeader(current, next) ? 3.0 : 0.0)
                + (continuation ? 1.5 : 0.0)
                + (current.SemanticMatches.Count * 3.5)
                + (distinctSemanticRoles * 1.5);

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static List<RowProfile> BuildPreviewBlock(IReadOnlyList<RowProfile> rows, int startIndex)
    {
        var block = new List<RowProfile> { rows[startIndex] };
        for (var index = startIndex + 1; index < rows.Count && block.Count < 12; index++)
        {
            var previous = block[^1];
            var current = rows[index];
            if (!AreAdjacent(previous, current))
                break;
            if (ColumnOverlap(previous, current) < 0.35)
                break;
            block.Add(current);
        }
        return block;
    }

    private static (int StartColumn, int EndColumn) ResolveActiveColumns(
        IReadOnlyList<RowProfile> block,
        RowProfile? header)
    {
        var occupancy = new Dictionary<int, int>();
        foreach (var row in block)
            foreach (var column in row.OccupiedColumns)
                occupancy[column] = occupancy.GetValueOrDefault(column) + 1;

        var threshold = block.Count == 1 ? 1 : Math.Max(2, (int)Math.Ceiling(block.Count * 0.50));
        var active = occupancy
            .Where(pair => pair.Value >= threshold)
            .Select(pair => pair.Key)
            .OrderBy(value => value)
            .ToList();
        if (active.Count == 0)
            active = block.SelectMany(row => row.OccupiedColumns).Distinct().OrderBy(value => value).ToList();

        var startColumn = active[0];
        var endColumn = active[^1];

        if (header is not null)
        {
            var dataOccupiedColumns = block
                .SelectMany(row => row.OccupiedColumns)
                .ToHashSet();

            while (header.OccupiedColumns.Contains(endColumn + 1)
                && dataOccupiedColumns.Contains(endColumn + 1))
            {
                endColumn++;
            }
        }

        return (startColumn, endColumn);
    }

    private static RowProfile AnalyzeRow(int rowNumber, IReadOnlyList<string> cells)
    {
        var occupied = new HashSet<int>();
        var nonBlank = new List<string>();
        for (var index = 0; index < cells.Count; index++)
        {
            var value = cells[index];
            if (string.IsNullOrWhiteSpace(value))
                continue;
            occupied.Add(index + 1);
            nonBlank.Add(value.Trim());
        }

        var scalarCount = nonBlank.Count(IsScalarLike);
        var textCount = nonBlank.Count - scalarCount;
        var isDataLike = nonBlank.Count > 0;
        var isLabelLike = nonBlank.Count >= 2
            && textCount >= Math.Ceiling(nonBlank.Count * 0.70)
            && nonBlank.Distinct(StringComparer.OrdinalIgnoreCase).Count() == nonBlank.Count;
        var semanticMatches = SemanticMatcher.MatchRow(cells);
        var headerTokenHits = nonBlank.Count(IsHeaderLabel);

        return new RowProfile(
            rowNumber,
            occupied,
            isLabelLike,
            isDataLike,
            scalarCount,
            nonBlank.Count,
            headerTokenHits,
            semanticMatches);
    }

    private static bool IsProbableHeader(RowProfile current, RowProfile next) =>
        current.IsLabelLike
        && next.IsDataLike
        && (current.SemanticMatches.Count > 0
            || current.HeaderTokenHits > 0
            || next.ScalarRatio >= current.ScalarRatio + 0.25);

    private static bool IsHeaderLabel(string value)
    {
        if (SemanticMatcher.Match(value) != ExcelSemanticFieldRole.Unknown)
            return true;

        var normalized = ExcelSemanticFieldMatcher.Normalize(value);
        string[] tokens =
        [
            "id", "kod", "code", "name", "ad", "adi", "aciklama", "description",
            "tutar", "amount", "total", "toplam", "tarih", "date", "adres", "address",
            "sehir", "city", "ulke", "country", "deger", "value"
        ];
        return tokens.Any(token => token.Length <= 2
            ? string.Equals(normalized, token, StringComparison.OrdinalIgnoreCase)
            : normalized.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsScalarLike(string value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)
        || decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out _)
        || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out _)
        || bool.TryParse(value, out _)
        || string.Equals(value, "EVET", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "HAYIR", StringComparison.OrdinalIgnoreCase);

    private static bool AreAdjacent(RowProfile left, RowProfile right) =>
        right.RowNumber == left.RowNumber + 1;

    private static double ColumnOverlap(RowProfile left, RowProfile right)
    {
        var denominator = Math.Min(left.OccupiedColumns.Count, right.OccupiedColumns.Count);
        return denominator == 0
            ? 0
            : left.OccupiedColumns.Intersect(right.OccupiedColumns).Count() / (double)denominator;
    }

    private sealed record RowProfile(
        int RowNumber,
        HashSet<int> OccupiedColumns,
        bool IsLabelLike,
        bool IsDataLike,
        int ScalarCount,
        int CellCount,
        int HeaderTokenHits,
        IReadOnlyList<ExcelSemanticFieldMatch> SemanticMatches)
    {
        public double ScalarRatio => CellCount == 0 ? 0 : ScalarCount / (double)CellCount;
    }
}
