namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// Produces a stable user-facing projection from raw Preview diagnostics.
/// Raw diagnostics remain available to rendering/debugging code, while repeated
/// occurrences of the same actionable problem become one warning-center item.
/// </summary>
public static class PreviewDiagnosticSummaryService
{
    private const string KeyPlaceholder = "…";

    public static IReadOnlyList<PreviewDiagnosticGroup> Group(
        IEnumerable<PreviewDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics
            .GroupBy(CreateKey)
            .Select(group => new PreviewDiagnosticGroup
            {
                Severity = group.Key.Severity,
                Title = group.Key.Title,
                Message = group.Key.MessageTemplate,
                ElementId = group.Key.ElementId,
                ElementName = group.Select(item => item.ElementName)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                OccurrenceCount = group.Count(),
                // Keep every distinct navigation key. The card renders only a
                // compact count, while repeated clicks can walk the complete set.
                KeyValues = group.Select(item => item.KeyValue)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Sources = group.SelectMany(item => item.Sources)
                    .GroupBy(CreateSourceKey)
                    .Select(sourceGroup => sourceGroup.First())
                    .ToList(),
                Representative = group.First()
            })
            .OrderBy(group => SeverityOrder(group.Severity))
            .ThenBy(group => group.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(group => group.ElementName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static int CountActionableGroups(IEnumerable<PreviewDiagnostic> diagnostics) =>
        Group(diagnostics).Count;

    private static PreviewDiagnosticGroupKey CreateKey(PreviewDiagnostic diagnostic) => new(
        diagnostic.Severity,
        Normalize(diagnostic.Title),
        NormalizeMessageTemplate(diagnostic),
        diagnostic.ElementId,
        Normalize(diagnostic.ElementName));

    /// <summary>
    /// Diagnostics deliberately retain their concrete key in the raw message for
    /// debugging and direct navigation. The warning-center grouping key must not
    /// treat that occurrence-specific value as a different root problem.
    /// </summary>
    private static string NormalizeMessageTemplate(PreviewDiagnostic diagnostic)
    {
        var message = Normalize(diagnostic.Message);
        var keyValue = Normalize(diagnostic.KeyValue);
        if (string.IsNullOrWhiteSpace(keyValue))
            return message;

        return message.Replace(
            keyValue,
            KeyPlaceholder,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateSourceKey(PreviewDiagnosticSource source) => string.Join("\u001f",
        Normalize(source.DataSourceName),
        Normalize(source.SourcePath),
        Normalize(source.WorksheetName),
        Normalize(source.RangeReference),
        Normalize(source.KeyColumnIdentity));

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static int SeverityOrder(PreviewDiagnosticSeverity severity) => severity switch
    {
        PreviewDiagnosticSeverity.Error => 0,
        PreviewDiagnosticSeverity.Warning => 1,
        _ => 2
    };

    private readonly record struct PreviewDiagnosticGroupKey(
        PreviewDiagnosticSeverity Severity,
        string Title,
        string MessageTemplate,
        Guid? ElementId,
        string ElementName);
}

public sealed class PreviewDiagnosticGroup
{
    public required PreviewDiagnosticSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public Guid? ElementId { get; init; }
    public string? ElementName { get; init; }
    public required int OccurrenceCount { get; init; }
    public IReadOnlyList<string> KeyValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PreviewDiagnosticSource> Sources { get; init; } = Array.Empty<PreviewDiagnosticSource>();
    public required PreviewDiagnostic Representative { get; init; }
}
