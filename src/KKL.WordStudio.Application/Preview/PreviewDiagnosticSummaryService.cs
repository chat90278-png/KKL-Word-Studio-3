namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// Produces a stable user-facing projection from raw Preview diagnostics.
/// Production diagnostics group by factory-owned semantic identity; a narrow
/// compatibility fallback remains for historical tests/callers without one.
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
            .Select(group =>
            {
                var representative = group.First();
                return new PreviewDiagnosticGroup
                {
                    Code = representative.Code,
                    Severity = group.Key.Severity,
                    Title = representative.Title,
                    Message = NormalizeMessageTemplate(representative),
                    ElementId = group.Key.ElementId,
                    ElementName = group.Select(item => item.ElementName)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    AffectedColumn = group.Select(item => item.AffectedColumn)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    OccurrenceCount = group.Count(),
                    KeyValues = group.Select(item => item.KeyValue)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => value!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(25)
                        .ToList(),
                    Sources = group.SelectMany(item => item.Sources)
                        .GroupBy(CreateSourceKey)
                        .Select(sourceGroup => sourceGroup.First())
                        .ToList(),
                    Representative = representative
                };
            })
            .OrderBy(group => SeverityOrder(group.Severity))
            .ThenBy(group => group.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(group => group.ElementName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static int CountActionableGroups(IEnumerable<PreviewDiagnostic> diagnostics) =>
        Group(diagnostics).Count;

    private static PreviewDiagnosticGroupKey CreateKey(PreviewDiagnostic diagnostic)
    {
        var semanticIdentity = string.IsNullOrWhiteSpace(diagnostic.GroupingKey)
            ? BuildLegacyIdentity(diagnostic)
            : Normalize(diagnostic.GroupingKey);

        return new PreviewDiagnosticGroupKey(
            diagnostic.Severity,
            Normalize(diagnostic.Code),
            semanticIdentity,
            diagnostic.ElementId,
            Normalize(diagnostic.ElementName));
    }

    private static string BuildLegacyIdentity(PreviewDiagnostic diagnostic) => string.Join(
        "\u001f",
        Normalize(diagnostic.Title),
        NormalizeMessageTemplate(diagnostic));

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
        string Code,
        string SemanticIdentity,
        Guid? ElementId,
        string ElementName);
}

public sealed class PreviewDiagnosticGroup
{
    public string Code { get; init; } = PreviewDiagnosticCodes.Unclassified;
    public required PreviewDiagnosticSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public Guid? ElementId { get; init; }
    public string? ElementName { get; init; }
    public string? AffectedColumn { get; init; }
    public required int OccurrenceCount { get; init; }
    public IReadOnlyList<string> KeyValues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PreviewDiagnosticSource> Sources { get; init; } = Array.Empty<PreviewDiagnosticSource>();
    public required PreviewDiagnostic Representative { get; init; }
}
