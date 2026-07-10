namespace KKL.WordStudio.Application.TableComposition;

using System.Globalization;
using System.Text;
using KKL.WordStudio.Domain.Elements;

internal static class ColumnRoleAliasNormalizer
{
    private static readonly HashSet<string> MatchKeyAliases = NormalizeAliases(
        "PN", "P/N", "Part No", "Part Number", "Product No", "Product Number",
        "Parça Numarası", "Parca Numarasi", "Ürün No", "Urun No");

    private static readonly HashSet<string> SerialAliases = NormalizeAliases(
        "Serial No", "Serial Number", "Seri No", "Seri Numarası", "Seri Numarasi", "S/N", "SN");

    private static readonly HashSet<string> QuantityAliases = NormalizeAliases(
        "Quantity", "Qty", "Adet", "Miktar");

    private static readonly HashSet<string> OrdinalAliases = NormalizeAliases(
        "#", "No", "No.", "Sıra No", "Sira No", "Row");

    public static bool MatchesMatchKey(TableColumn column) => Matches(column, MatchKeyAliases);

    public static bool MatchesSerial(TableColumn column) => Matches(column, SerialAliases);

    public static bool MatchesQuantity(TableColumn column) => Matches(column, QuantityAliases);

    public static bool MatchesOrdinal(TableColumn column) => Matches(column, OrdinalAliases);

    private static bool Matches(TableColumn column, HashSet<string> aliases) =>
        aliases.Contains(Normalize(column.Header))
        || (!string.IsNullOrWhiteSpace(column.SourceField) && aliases.Contains(Normalize(column.SourceField)));

    private static HashSet<string> NormalizeAliases(params string[] aliases) =>
        aliases.Select(Normalize).ToHashSet(StringComparer.Ordinal);

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed == "#")
            return "#";

        var decomposed = trimmed.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var lowerCharacter = char.ToLowerInvariant(character);
            var normalizedCharacter = lowerCharacter == 'ı' ? 'i' : lowerCharacter;
            if (char.IsLetterOrDigit(normalizedCharacter))
                builder.Append(normalizedCharacter);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
