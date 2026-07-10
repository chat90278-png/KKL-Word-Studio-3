namespace KKL.WordStudio.Application.Excel;

using System.Globalization;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Small deterministic range heuristic. It ignores leading blank rows, looks
/// for adjacent rows with substantially overlapping occupied columns, prefers
/// a label-like row as a header when followed by a data-like row, and derives
/// active column bounds from columns repeatedly occupied by the detected block.
/// It intentionally does not claim perfect Excel-table inference.
/// </summary>
public sealed class ExcelDataRangeDetector : IExcelDataRangeDetector
{
    private const double StrongOverlap = 0.60;

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
        var hasHeader = next is not null
            && AreAdjacent(current, next)
            && overlap >= StrongOverlap
            && IsProbableHeader(current, next);

        var dataStartIndex = hasHeader ? startIndex + 1 : startIndex;
        var dataStart = rows[dataStartIndex];
        var block = BuildPreviewBlock(rows, dataStartIndex);
        var (startColumn, endColumn) = ResolveActiveColumns(block, hasHeader ? current : null);

        var strongContinuation = block.Count >= 2
            && block.Zip(block.Skip(1), (left, right) => AreAdjacent(left, right) && ColumnOverlap(left, right) >= StrongOverlap)
                .Any(value => value);
        var hasUnambiguousDataShape = block.Any(row => row.ScalarRatio > 0 || !row.IsLabelLike);
        var confidence = (hasHeader && overlap >= 0.75) || (strongContinuation && hasUnambiguousDataShape)
            ? ExcelDataRangeConfidence.High
            : ExcelDataRangeConfidence.Low;

        return Result.Success(new ExcelDataRangeCandidate
        {
            HeaderRowIndex = hasHeader ? current.RowNumber : null,
            DataStartRow = dataStart.RowNumber,
            StartColumn = startColumn,
            EndColumn = endColumn,
            Confidence = confidence
        });
    }

    private static int FindBestStart(IReadOnlyList<RowProfile> rows)
    {
        var bestIndex = 0;
        var bestScore = double.MinValue;

        for (var index = 0; index < rows.Count - 1; index++)
        {
            var current = rows[index];
            var next = rows[index + 1];
            if (!AreAdjacent(current, next)) continue;

            var overlap = ColumnOverlap(current, next);
            if (overlap < StrongOverlap) continue;

            var density = Math.Min(current.OccupiedColumns.Count, next.OccupiedColumns.Count);
            var continuation = index + 2 < rows.Count
                && AreAdjacent(next, rows[index + 2])
                && ColumnOverlap(next, rows[index + 2]) >= StrongOverlap;
            var score = (density * 2.0)
                + (overlap * 2.0)
                + (IsProbableHeader(current, next) ? 3.0 : 0.0)
                + (continuation ? 1.5 : 0.0);

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
            if (!AreAdjacent(previous, current)) break;
            if (ColumnOverlap(previous, current) < 0.35) break;
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
        var active = occupancy.Where(pair => pair.Value >= threshold).Select(pair => pair.Key).OrderBy(value => value).ToList();
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
            if (string.IsNullOrWhiteSpace(value)) continue;
            occupied.Add(index + 1);
            nonBlank.Add(value.Trim());
        }

        var scalarCount = nonBlank.Count(IsScalarLike);
        var textCount = nonBlank.Count - scalarCount;
        var isDataLike = nonBlank.Count > 0;
        var isLabelLike = nonBlank.Count >= 2
            && textCount >= Math.Ceiling(nonBlank.Count * 0.70)
            && nonBlank.Distinct(StringComparer.OrdinalIgnoreCase).Count() == nonBlank.Count;
        var headerTokenHits = nonBlank.Count(IsHeaderLabel);

        return new RowProfile(rowNumber, occupied, isLabelLike, isDataLike, scalarCount, nonBlank.Count, headerTokenHits);
    }

    private static bool IsProbableHeader(RowProfile current, RowProfile next) =>
        current.IsLabelLike
        && next.IsDataLike
        && (current.HeaderTokenHits > 0 || next.ScalarRatio >= current.ScalarRatio + 0.25);

    private static bool IsHeaderLabel(string value)
    {
        var normalized = new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        string[] tokens =
        [
            "id", "kod", "code", "name", "ad", "adi", "adı", "açıklama", "aciklama", "description",
            "miktar", "quantity", "qty", "tutar", "amount", "total", "toplam", "tarih", "date", "no", "numara",
            "adres", "address", "şehir", "sehir", "city", "ülke", "ulke", "country", "değer", "deger", "value"
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

    private static bool AreAdjacent(RowProfile left, RowProfile right) => right.RowNumber == left.RowNumber + 1;

    private static double ColumnOverlap(RowProfile left, RowProfile right)
    {
        var denominator = Math.Min(left.OccupiedColumns.Count, right.OccupiedColumns.Count);
        return denominator == 0 ? 0 : left.OccupiedColumns.Intersect(right.OccupiedColumns).Count() / (double)denominator;
    }

    private sealed record RowProfile(
        int RowNumber,
        HashSet<int> OccupiedColumns,
        bool IsLabelLike,
        bool IsDataLike,
        int ScalarCount,
        int CellCount,
        int HeaderTokenHits)
    {
        public double ScalarRatio => CellCount == 0 ? 0 : ScalarCount / (double)CellCount;
    }
}
