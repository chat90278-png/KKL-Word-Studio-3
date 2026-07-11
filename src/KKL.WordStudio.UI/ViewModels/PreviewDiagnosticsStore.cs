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
    public bool HasItems => Count > 0;
    public string CountText => Count == 1 ? "1" : Count.ToString();

    public void Replace(IReadOnlyList<PreviewDiagnostic> diagnostics)
    {
        Items.Clear();
        foreach (var diagnostic in diagnostics)
            Items.Add(diagnostic);

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(CountText));
    }

    public void Clear() => Replace(Array.Empty<PreviewDiagnostic>());
}
