namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using KKL.WordStudio.Application.Preview;

/// <summary>
/// Runtime-only bridge between Preview rendering and the diagnostics dock.
/// No diagnostic history is persisted in Project or Report.
/// </summary>
public sealed class PreviewDiagnosticsStore : ViewModelBase
{
    public ObservableCollection<PreviewDiagnostic> Items { get; } = new();

    public int Count => Items.Count;
    public int TotalOccurrenceCount => Items.Sum(item => Math.Max(1, item.OccurrenceCount));
    public int ErrorCount => Items.Count(item => item.Severity == PreviewDiagnosticSeverity.Error);
    public int WarningCount => Items.Count(item => item.Severity == PreviewDiagnosticSeverity.Warning);
    public int InformationCount => Items.Count(item => item.Severity == PreviewDiagnosticSeverity.Information);
    public bool HasItems => Count > 0;
    public bool HasErrors => ErrorCount > 0;
    public string CountText => Count == 1 ? "1" : Count.ToString();
    public string BadgeBackground => HasErrors ? "#FFFFD6D6" : "#FFFFE3A1";
    public string BadgeForeground => HasErrors ? "#FFB42318" : "#FF8A5A00";

    public void Replace(IReadOnlyList<PreviewDiagnostic> diagnostics)
    {
        Items.Clear();
        foreach (var diagnostic in PreviewDiagnosticConsolidator.Consolidate(diagnostics))
            Items.Add(diagnostic);

        PublishSummaryProperties();
    }

    public void Clear() => Replace(Array.Empty<PreviewDiagnostic>());

    private void PublishSummaryProperties()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(TotalOccurrenceCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(InformationCount));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(BadgeBackground));
        OnPropertyChanged(nameof(BadgeForeground));
    }
}
