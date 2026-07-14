namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// Produces a stable user-facing projection from raw Preview diagnostics.
/// Catalogued diagnostics group by stable code + report element + affected
/// column; legacy diagnostics retain the prior normalized-message grouping.
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
                var first = group.First();
                var keyValues = group
                    .Select(item => item.KeyValue)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(100)
                    .ToList();
                var rows = group
                    .Where(item => item.RowNumber.HasValue)
                    .Select(item => item.RowNumber!.Value)
                    .Distinct()
                    .OrderBy(row => row)
                    .Take(100)
                    .ToList();
                var sources = group
                    .SelectMany(item => item.Sources)
                    .GroupBy(CreateSourceKey)
                    .Select(sourceGroup => sourceGroup.First())
                    .ToList();
                var occurrenceCount = group.Count();
                var messageTemplate = NormalizeMessageTemplate(first);

                return new PreviewDiagnosticGroup
                {
                    Code = first.Code,
                    Severity = group.Key.Severity,
                    Title = first.Title,
                    Message = string.Equals(first.Code, PreviewDiagnosticCodes.Unclassified, StringComparison.Ordinal)
                        ? messageTemplate
                        : PreviewDiagnosticCatalog.BuildGroupMessage(
                            first.Code,
                            occurrenceCount,
                            first.AffectedColumn,
                            messageTemplate),
                    ElementId = group.Key.ElementId,
                    ElementName = group.Select(item => item.ElementName)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    AffectedColumn = group.Select(item => item.AffectedColumn)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    OccurrenceCount = occurrenceCount,
                    KeyValues = keyValues,
                    RowNumbers = rows,
                    Sources = sources,
                    Representative = first
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
        var catalogued = !string.Equals(
            diagnostic.Code,
            PreviewDiagnosticCodes.Unclassified,
            StringComparison.Ordinal);

        return new PreviewDiagnosticGroupKey(
            diagnostic.Severity,
            diagnostic.ElementId,
            catalogued ? Normalize(diagnostic.Code) : string.Empty,
            catalogued ? string.Empty : Normalize(diagnostic.Title),
            catalogued ? string.Empty : NormalizeMessageTemplate(diagnostic),
            Normalize(diagnostic.AffectedColumn),
            Normalize(diagnostic.ElementName));
    }

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
        Guid? ElementId,
        string Code,
        string LegacyTitle,
        string LegacyMessageTemplate,
        string AffectedColumn,
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
    public IReadOnlyList<int> RowNumbers { get; init; } = Array.Empty<int>();
    public IReadOnlyList<PreviewDiagnosticSource> Sources { get; init; } = Array.Empty<PreviewDiagnosticSource>();
    public required PreviewDiagnostic Representative { get; init; }
}
