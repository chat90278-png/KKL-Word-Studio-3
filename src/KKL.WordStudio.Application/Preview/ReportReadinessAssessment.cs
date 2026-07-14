namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// Export-readiness projection over the existing grouped Preview diagnostics.
/// It introduces no second validator; PreviewDiagnosticSummaryService remains
/// the authoritative grouping source used by both the Control center and export.
/// </summary>
public sealed class ReportReadinessAssessment
{
    public required int ErrorGroupCount { get; init; }
    public required int WarningGroupCount { get; init; }
    public required int InformationGroupCount { get; init; }
    public required int ErrorOccurrenceCount { get; init; }
    public required int WarningOccurrenceCount { get; init; }
    public required int InformationOccurrenceCount { get; init; }

    public int TotalGroupCount => ErrorGroupCount + WarningGroupCount + InformationGroupCount;
    public int TotalOccurrenceCount => ErrorOccurrenceCount + WarningOccurrenceCount + InformationOccurrenceCount;
    public bool BlocksExport => ErrorGroupCount > 0;
    public bool RequiresWarningConfirmation => !BlocksExport && WarningGroupCount > 0;

    public static ReportReadinessAssessment FromGroups(IEnumerable<PreviewDiagnosticGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var materialized = groups.ToList();

        return new ReportReadinessAssessment
        {
            ErrorGroupCount = CountGroups(materialized, PreviewDiagnosticSeverity.Error),
            WarningGroupCount = CountGroups(materialized, PreviewDiagnosticSeverity.Warning),
            InformationGroupCount = CountGroups(materialized, PreviewDiagnosticSeverity.Information),
            ErrorOccurrenceCount = CountOccurrences(materialized, PreviewDiagnosticSeverity.Error),
            WarningOccurrenceCount = CountOccurrences(materialized, PreviewDiagnosticSeverity.Warning),
            InformationOccurrenceCount = CountOccurrences(materialized, PreviewDiagnosticSeverity.Information)
        };
    }

    public static ReportReadinessAssessment FromDiagnostics(IEnumerable<PreviewDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return FromGroups(PreviewDiagnosticSummaryService.Group(diagnostics));
    }

    private static int CountGroups(
        IEnumerable<PreviewDiagnosticGroup> groups,
        PreviewDiagnosticSeverity severity) => groups.Count(group => group.Severity == severity);

    private static int CountOccurrences(
        IEnumerable<PreviewDiagnosticGroup> groups,
        PreviewDiagnosticSeverity severity) => groups
        .Where(group => group.Severity == severity)
        .Sum(group => Math.Max(1, group.OccurrenceCount));
}
