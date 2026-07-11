namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Owns the right Context Dock's physical state and active page. This state is
/// UI-only and is never persisted in the report/project model.
/// </summary>
public sealed partial class DockViewModel : ViewModelBase
{
    [ObservableProperty]
    private DockState _state = DockState.Normal;

    [ObservableProperty]
    private DockPage _page = DockPage.Contents;

    public DockViewModel()
        : this(new PreviewDiagnosticsStore())
    {
    }

    public DockViewModel(PreviewDiagnosticsStore diagnostics)
    {
        Diagnostics = diagnostics;
    }

    public PreviewDiagnosticsStore Diagnostics { get; }
    public double NormalWidth => 350;
    public double CollapsedWidth => 46;
    public double ExpandedWidth => 440;

    [RelayCommand]
    private void ShowContents() => Show(DockPage.Contents);

    [RelayCommand]
    private void ShowProperties() => Show(DockPage.Properties);

    [RelayCommand]
    private void ShowWarnings() => Show(DockPage.Warnings);

    [RelayCommand]
    private void ShowChangeBinding() => Show(DockPage.ChangeBinding);

    [RelayCommand]
    private void Collapse() => State = DockState.Collapsed;

    [RelayCommand]
    private void Expand() => State = State == DockState.Expanded ? DockState.Normal : DockState.Expanded;

    [RelayCommand]
    private void RestoreToContents() => Show(DockPage.Contents);

    [RelayCommand]
    private void RestoreToProperties() => Show(DockPage.Properties);

    [RelayCommand]
    private void RestoreToWarnings() => Show(DockPage.Warnings);

    private void Show(DockPage page)
    {
        State = DockState.Normal;
        Page = page;
    }
}
