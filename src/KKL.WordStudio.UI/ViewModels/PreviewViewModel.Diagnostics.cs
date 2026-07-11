namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Preview;

public sealed partial class PreviewViewModel
{
    public ObservableCollection<PreviewDiagnostic> Diagnostics { get; } = new();

    public int WarningCount => Diagnostics.Count;
    public bool HasWarnings => WarningCount > 0;
    public string WarningCountText => WarningCount == 1 ? "1 uyarı" : $"{WarningCount} uyarı";

    public event Action? OpenWarningsRequested;
    public event Action<Guid>? NavigateToElementRequested;

    [RelayCommand]
    private void OpenWarnings() => OpenWarningsRequested?.Invoke();

    public void NavigateToElement(Guid elementId)
    {
        _workspace.SetSelectedReportElement(elementId);
        NavigateToElementRequested?.Invoke(elementId);
    }

    private void ReplaceDiagnostics(IReadOnlyList<PreviewDiagnostic> diagnostics)
    {
        Diagnostics.Clear();
        foreach (var diagnostic in diagnostics)
            Diagnostics.Add(diagnostic);

        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(WarningCountText));
    }
}
