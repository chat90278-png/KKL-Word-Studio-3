namespace KKL.WordStudio.Application.Excel;

using System.Globalization;
using System.Text;

/// <summary>
/// Canonical report fields recognised from Turkish/English worksheet headers.
/// The role is semantic; the original user-visible header text is preserved
/// separately and may be edited without losing this identity.
/// </summary>
public enum ExcelSemanticFieldRole
{
    Unknown = 0,
    ItemNumber,
    PartNameEnglish,
    PartNameTurkish,
    PartNumber,
    Nsn,
    SerialNumber,
    Quantity
}

public sealed record ExcelSemanticFieldMatch(
    int ColumnIndex,
    string HeaderText,
    ExcelSemanticFieldRole Role,
    int Confidence);

public interface IExcelSemanticFieldMatcher
{
    ExcelSemanticFieldRole Match(string? headerText);
    IReadOnlyList<ExcelSemanticFieldMatch> MatchRow(IReadOnlyList<string> cells);
}

/// <summary>
/// Deterministic, culture-safe alias matcher. It deliberately uses an explicit
/// vocabulary instead of fuzzy positional guessing so an unknown column remains
/// available for manual selection rather than being silently misclassified.
/// </summary>
public sealed class ExcelSemanticFieldMatcher : IExcelSemanticFieldMatcher
{
    private static readonly IReadOnlyDictionary<string, ExcelSemanticFieldRole> Aliases =
        BuildAliases();

    public ExcelSemanticFieldRole Match(string? headerText)
    {
        var normalized = Normalize(headerText);
        return normalized.Length > 0 && Aliases.TryGetValue(normalized, out var role)
            ? role
            : ExcelSemanticFieldRole.Unknown;
    }

    public IReadOnlyList<ExcelSemanticFieldMatch> MatchRow(IReadOnlyList<string> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        var matches = new List<ExcelSemanticFieldMatch>();
        for (var index = 0; index < cells.Count; index++)
        {
            var header = cells[index];
            var role = Match(header);
            if (role == ExcelSemanticFieldRole.Unknown)
                continue;

            matches.Add(new ExcelSemanticFieldMatch(
                ColumnIndex: index + 1,
                HeaderText: header,
                Role: role,
                Confidence: 100));
        }

        return matches;
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (!char.IsLetterOrDigit(character))
                continue;

            var lower = char.ToLowerInvariant(character);
            builder.Append(lower == 'ı' ? 'i' : lower);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IReadOnlyDictionary<string, ExcelSemanticFieldRole> BuildAliases()
    {
        var aliases = new Dictionary<string, ExcelSemanticFieldRole>(StringComparer.Ordinal);

        Add(ExcelSemanticFieldRole.ItemNumber,
            "No", "No.", "Number", "Numara", "Item No", "Item Number", "Sıra No", "Sira No");

        Add(ExcelSemanticFieldRole.PartNameEnglish,
            "Part Name", "Part Name (English)", "English Part Name", "Parça Adı İngilizce",
            "İngilizce Parça Adı", "En İsim", "EN Name");

        Add(ExcelSemanticFieldRole.PartNameTurkish,
            "Parça Adı", "Parça Adı (Türkçe)", "Türkçe Parça Adı", "Tr İsim", "TR Name");

        Add(ExcelSemanticFieldRole.PartNumber,
            "Part No", "Part No.", "Part Number", "Parça No", "Parça Numarası");

        Add(ExcelSemanticFieldRole.Nsn,
            "NSN", "N.S.N.");

        Add(ExcelSemanticFieldRole.SerialNumber,
            "Serial No", "Serial No.", "Serial Number", "Seri No", "Seri Numarası");

        Add(ExcelSemanticFieldRole.Quantity,
            "Qty", "Quantity", "Adet", "Miktar");

        return aliases;

        void Add(ExcelSemanticFieldRole role, params string[] values)
        {
            foreach (var value in values)
                aliases[Normalize(value)] = role;
        }
    }
}
