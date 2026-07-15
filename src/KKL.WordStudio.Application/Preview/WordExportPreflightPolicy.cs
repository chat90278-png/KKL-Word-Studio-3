namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// Decides whether the latest structured Preview findings permit Word export.
/// It consumes the existing grouped diagnostic projection and never revalidates
/// report data, classifies message text, or changes exporter behavior.
/// </summary>
public static class WordExportPreflightPolicy
{
    public static WordExportPreflightResult Evaluate(
        IEnumerable<PreviewDiagnosticGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        var snapshot = groups.ToList();
        var errorGroups = snapshot
            .Where(group => group.Severity == PreviewDiagnosticSeverity.Error)
            .ToList();
        var warningGroups = snapshot
            .Where(group => group.Severity == PreviewDiagnosticSeverity.Warning)
            .ToList();
        var informationGroups = snapshot
            .Where(group => group.Severity == PreviewDiagnosticSeverity.Information)
            .ToList();

        var status = errorGroups.Count > 0
            ? WordExportPreflightStatus.Blocked
            : warningGroups.Count > 0 || informationGroups.Count > 0
                ? WordExportPreflightStatus.ReadyWithFindings
                : WordExportPreflightStatus.Ready;

        return new WordExportPreflightResult(
            status,
            errorGroups.Count,
            warningGroups.Count,
            informationGroups.Count,
            CountFindings(errorGroups),
            CountFindings(warningGroups),
            CountFindings(informationGroups));
    }

    private static int CountFindings(IEnumerable<PreviewDiagnosticGroup> groups) =>
        groups.Sum(group => Math.Max(0, group.OccurrenceCount));
}

public enum WordExportPreflightStatus
{
    Ready,
    ReadyWithFindings,
    Blocked
}

public sealed record WordExportPreflightResult(
    WordExportPreflightStatus Status,
    int ErrorGroupCount,
    int WarningGroupCount,
    int InformationGroupCount,
    int ErrorFindingCount,
    int WarningFindingCount,
    int InformationFindingCount)
{
    public bool CanExport => Status != WordExportPreflightStatus.Blocked;
    public int GroupCount => ErrorGroupCount + WarningGroupCount + InformationGroupCount;
    public int FindingCount => ErrorFindingCount + WarningFindingCount + InformationFindingCount;
    public int NonBlockingGroupCount => WarningGroupCount + InformationGroupCount;
    public int NonBlockingFindingCount => WarningFindingCount + InformationFindingCount;
}
