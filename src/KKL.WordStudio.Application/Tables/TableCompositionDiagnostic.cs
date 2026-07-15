namespace KKL.WordStudio.Application.Tables;

using System.Text.RegularExpressions;

/// <summary>
/// Stable semantic identity for one table-composition finding. The original
/// technical message is retained for support/debugging, while code and metadata
/// drive Preview grouping, navigation and export-readiness decisions.
/// </summary>
public sealed record TableCompositionDiagnostic(
    string Code,
    string Message,
    string? KeyValue = null,
    string? AffectedColumn = null);

public static class TableCompositionDiagnosticCodes
{
    public const string ConfigurationInvalid = "TABLE_GROUPING_CONFIGURATION_INVALID";
    public const string QuantityMissing = "TABLE_QUANTITY_MISSING";
    public const string QuantityInvalid = "TABLE_QUANTITY_INVALID";
    public const string QuantityConflicting = "TABLE_QUANTITY_CONFLICTING";
    public const string MergeConflict = "TABLE_MERGE_CONFLICT";
    public const string SerialDuplicate = "TABLE_SERIAL_DUPLICATE";
    public const string SerialQuantityMismatch = "TABLE_SERIAL_QUANTITY_MISMATCH";
    public const string LegacyWarning = "TABLE_LEGACY_WARNING";
}

/// <summary>
/// Compatibility boundary for existing composers that still expose warning
/// text. Message parsing is isolated here; Preview and UI consumers receive
/// structured diagnostics and never classify localized text themselves.
/// </summary>
public static class TableCompositionDiagnosticClassifier
{
    private static readonly Regex KeyRegex = new(
        "PN/key\\s+'(?<key>[^']+)'",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ColumnRegex = new(
        "için\\s+'(?<column>[^']+)'\\s+alanında",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<TableCompositionDiagnostic> Classify(
        IEnumerable<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        return warnings
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => ClassifyOne(message.Trim()))
            .ToList();
    }

    private static TableCompositionDiagnostic ClassifyOne(string message)
    {
        var key = Extract(KeyRegex, message, "key");
        var column = Extract(ColumnRegex, message, "column");

        if (message.Contains("yapılandırması geçersiz", StringComparison.OrdinalIgnoreCase))
            return new(TableCompositionDiagnosticCodes.ConfigurationInvalid, message);

        if (message.Contains("geçersiz Adet değeri", StringComparison.OrdinalIgnoreCase))
            return new(TableCompositionDiagnosticCodes.QuantityInvalid, message, key, "Adet");

        if (message.Contains("çelişkili Adet değerleri", StringComparison.OrdinalIgnoreCase))
            return new(TableCompositionDiagnosticCodes.QuantityConflicting, message, key, "Adet");

        if (message.Contains("geçerli Adet değeri bulunamadı", StringComparison.OrdinalIgnoreCase))
            return new(TableCompositionDiagnosticCodes.QuantityMissing, message, key, "Adet");

        if (message.Contains("alanında çelişkili değerler", StringComparison.OrdinalIgnoreCase))
            return new(TableCompositionDiagnosticCodes.MergeConflict, message, key, column);

        if (message.Contains("tekrarlanan Seri No", StringComparison.OrdinalIgnoreCase))
            return new(TableCompositionDiagnosticCodes.SerialDuplicate, message, key, "Seri No");

        if (message.Contains("eşleşen Seri No", StringComparison.OrdinalIgnoreCase))
            return new(TableCompositionDiagnosticCodes.SerialQuantityMismatch, message, key, "Seri No");

        return new(TableCompositionDiagnosticCodes.LegacyWarning, message, key, column);
    }

    private static string? Extract(Regex regex, string message, string groupName)
    {
        var match = regex.Match(message);
        return match.Success ? match.Groups[groupName].Value : null;
    }
}
