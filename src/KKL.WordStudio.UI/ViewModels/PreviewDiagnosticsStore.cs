namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using KKL.WordStudio.Application.Preview;

/// <summary>
/// Runtime-only bridge between Preview rendering and the diagnostics dock.
/// Raw diagnostics stay available, while summary properties expose deduplicated
/// actionable groups to the shell badge and export guards.
/// </summary>
public sealed class PreviewDiagnosticsStore : ViewModelBase
{
    public ObservableCollection<PreviewDiagnostic> Items { get; } = new();

    public int RawCount => Items.Count;
    public IReadOnlyList<PreviewDiagnosticGroup> Groups => PreviewDiagnosticSummaryService.Group(Items);
    public int Count => Groups.Count;
    public int FindingCount => Groups.Sum(group => group.OccurrenceCount);
    public int ErrorCount => Groups.Count(group => group.Severity == PreviewDiagnosticSeverity.Error);
    public int WarningCount => Groups.Count(group => group.Severity == PreviewDiagnosticSeverity.Warning);
    public int InformationCount => Groups.Count(group => group.Severity == PreviewDiagnosticSeverity.Information);
    public bool HasItems => Count > 0;
    public bool HasBlockingErrors => ErrorCount > 0;
    public string CountText => Count > 99 ? "99+" : Count.ToString();
    public string BadgeBackground => HasBlockingErrors ? "#FFF8D7DA" : "#FFFFE3A1";
    public string BadgeForeground => HasBlockingErrors ? "#FF9B1C31" : "#FF8A5A00";

    public void Replace(IReadOnlyList<PreviewDiagnostic> diagnostics)
    {
        Items.Clear();
        foreach (var diagnostic in diagnostics)
            Items.Add(diagnostic);

        PublishSummary();
    }

    public void Clear() => Replace(Array.Empty<PreviewDiagnostic>());

    private void PublishSummary()
    {
        OnPropertyChanged(nameof(RawCount));
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(FindingCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(InformationCount));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasBlockingErrors));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(BadgeBackground));
        OnPropertyChanged(nameof(BadgeForeground));
    }
}
