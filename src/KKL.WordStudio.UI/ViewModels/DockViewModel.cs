namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Owns the right Context Dock's physical state (Normal/Collapsed/Expanded)
/// and which page it's showing (Contents/Properties/ChangeBinding).
/// Deliberately UI-only — the Variant 2.5 task is explicit that dock state
/// does not belong in Domain.
/// </summary>
public sealed partial class DockViewModel : ViewModelBase
{
    [ObservableProperty]
    private DockState _state = DockState.Normal;

    [ObservableProperty]
    private DockPage _page = DockPage.Contents;

    public double NormalWidth => 350;
    public double CollapsedWidth => 46;
    public double ExpandedWidth => 440;

    [RelayCommand]
    private void ShowContents() => Page = DockPage.Contents;

    [RelayCommand]
    private void ShowProperties() => Page = DockPage.Properties;

    [RelayCommand]
    private void ShowChangeBinding() => Page = DockPage.ChangeBinding;

    [RelayCommand]
    private void Collapse() => State = DockState.Collapsed;

    [RelayCommand]
    private void Expand() => State = State == DockState.Expanded ? DockState.Normal : DockState.Expanded;

    [RelayCommand]
    private void RestoreToContents()
    {
        State = DockState.Normal;
        Page = DockPage.Contents;
    }

    [RelayCommand]
    private void RestoreToProperties()
    {
        State = DockState.Normal;
        Page = DockPage.Properties;
    }
}
