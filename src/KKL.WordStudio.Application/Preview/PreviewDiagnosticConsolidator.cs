namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// Collapses repeated Preview findings into one navigable diagnostic group.
/// Preview remains the single source of truth; this class only removes UI noise
/// and computes export-readiness counts from that existing stream.
/// </summary>
public static class PreviewDiagnosticConsolidator
{
    public static IReadOnlyList<PreviewDiagnostic> Consolidate(IEnumerable<PreviewDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return diagnostics
            .GroupBy(diagnostic => new DiagnosticGroupKey(
                diagnostic.Severity,
                diagnostic.ElementId,
                Normalize(diagnostic.Title),
                Normalize(diagnostic.Message),
                Normalize(diagnostic.ElementName)))
            .Select(group =>
            {
                var first = group.First();
                var keys = group
                    .Select(diagnostic => diagnostic.KeyValue)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var sources = group
                    .SelectMany(diagnostic => diagnostic.Sources)
                    .DistinctBy(SourceIdentity)
                    .ToList();

                return new PreviewDiagnostic
                {
                    Id = first.Id,
                    Severity = first.Severity,
                    Title = first.Title,
                    Message = first.Message,
                    ElementId = first.ElementId,
                    ElementName = first.ElementName,
                    KeyValue = keys.Count == 1 ? keys[0] : null,
                    Sources = sources,
                    OccurrenceCount = group.Sum(diagnostic => Math.Max(1, diagnostic.OccurrenceCount))
                };
            })
            .OrderByDescending(diagnostic => diagnostic.Severity)
            .ThenBy(diagnostic => diagnostic.ElementName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(diagnostic => diagnostic.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static string SourceIdentity(PreviewDiagnosticSource source) => string.Join(
        "\u001f",
        source.DataSourceName,
        source.SourcePath,
        source.WorksheetName,
        source.RangeReference,
        source.KeyColumnIdentity);

    private sealed record DiagnosticGroupKey(
        PreviewDiagnosticSeverity Severity,
        Guid? ElementId,
        string Title,
        string Message,
        string ElementName);
}

public sealed class ReportReadinessAssessment
{
    public required int ErrorGroupCount { get; init; }
    public required int WarningGroupCount { get; init; }
    public required int InformationGroupCount { get; init; }
    public required int TotalOccurrenceCount { get; init; }

    public bool BlocksExport => ErrorGroupCount > 0;
    public bool RequiresWarningConfirmation => !BlocksExport && WarningGroupCount > 0;

    public static ReportReadinessAssessment From(IEnumerable<PreviewDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var consolidated = PreviewDiagnosticConsolidator.Consolidate(diagnostics);
        return new ReportReadinessAssessment
        {
            ErrorGroupCount = consolidated.Count(item => item.Severity == PreviewDiagnosticSeverity.Error),
            WarningGroupCount = consolidated.Count(item => item.Severity == PreviewDiagnosticSeverity.Warning),
            InformationGroupCount = consolidated.Count(item => item.Severity == PreviewDiagnosticSeverity.Information),
            TotalOccurrenceCount = consolidated.Sum(item => Math.Max(1, item.OccurrenceCount))
        };
    }
}
