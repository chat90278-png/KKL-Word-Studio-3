namespace KKL.WordStudio.Application.Importing;

/// <summary>
/// File-type and multi-file drop decisions shared by WPF gesture handlers and
/// tests. No pixel/UI state lives here; Views only route OS drag/drop events.
/// </summary>
public static class SourceFileDropValidator
{
    private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xlsm"
    };

    public static bool IsSupportedExcelFile(string filePath) =>
        ExcelExtensions.Contains(Path.GetExtension(filePath));

    public static bool IsSupportedFrontMatterFile(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".docx", StringComparison.OrdinalIgnoreCase);

    public static SourceDropDecision EvaluateExcelDrop(IReadOnlyList<string> filePaths)
    {
        var firstValid = filePaths.FirstOrDefault(IsSupportedExcelFile);
        if (firstValid is null)
        {
            return SourceDropDecision.Rejected(
                "Bu alana yalnızca .xlsx veya .xlsm Excel dosyaları bırakılabilir.");
        }

        var validCount = filePaths.Count(IsSupportedExcelFile);
        var message = validCount > 1
            ? "Birden fazla Excel dosyası bırakıldı. Bu sürümde ilk desteklenen dosya açıldı."
            : null;

        return SourceDropDecision.Accepted(firstValid, message);
    }

    public static SourceDropDecision EvaluateFrontMatterDrop(IReadOnlyList<string> filePaths)
    {
        var firstValid = filePaths.FirstOrDefault(IsSupportedFrontMatterFile);
        if (firstValid is null)
            return SourceDropDecision.Rejected("Bu alana yalnızca .docx Word belgesi bırakılabilir.");

        var validCount = filePaths.Count(IsSupportedFrontMatterFile);
        var message = validCount > 1
            ? "Birden fazla Word belgesi bırakıldı. Bu sürümde ilk .docx ön belge olarak kullanıldı."
            : null;

        return SourceDropDecision.Accepted(firstValid, message);
    }
}

public sealed record SourceDropDecision(bool IsAccepted, string? FilePath, string? Message)
{
    public static SourceDropDecision Accepted(string filePath, string? message = null) =>
        new(true, filePath, message);

    public static SourceDropDecision Rejected(string message) =>
        new(false, null, message);
}
