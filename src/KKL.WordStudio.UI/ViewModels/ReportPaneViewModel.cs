namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Session-only shell state for the combined Preview + Context Dock workspace.
/// The state is shared by MainWindow, Excel transfer and Contents navigation so
/// successful report actions can reveal the report without rebuilding Preview.
/// </summary>
public sealed partial class ReportPaneViewModel : ViewModelBase
{
    public static ReportPaneViewModel Shared { get; } = new();

    public const double SmallViewportThreshold = 1180;
    public const double WideViewportThreshold = 1500;
    public const double MinimumOpenWidth = 560;
    public const double MaximumOpenWidth = 980;

    private bool _hasUserPreference;

    [ObservableProperty]
    private bool _isOpen = true;

    [ObservableProperty]
    private double _openWidth = 760;

    [ObservableProperty]
    private string _toggleText = "›";

    [ObservableProperty]
    private string _toggleToolTip = "Rapor alanını kapat";

    [RelayCommand]
    public void Toggle()
    {
        _hasUserPreference = true;
        IsOpen = !IsOpen;
    }

    [RelayCommand]
    public void Open()
    {
        _hasUserPreference = true;
        IsOpen = true;
    }

    [RelayCommand]
    public void Close()
    {
        _hasUserPreference = true;
        IsOpen = false;
    }

    public void OpenForAction() => IsOpen = true;

    public void ApplyViewportWidth(double viewportWidth)
    {
        if (!double.IsFinite(viewportWidth) || viewportWidth <= 0)
            return;

        OpenWidth = Math.Clamp(viewportWidth * 0.56, MinimumOpenWidth, MaximumOpenWidth);
        if (_hasUserPreference)
            return;

        if (viewportWidth < SmallViewportThreshold)
            IsOpen = false;
        else if (viewportWidth >= WideViewportThreshold)
            IsOpen = true;
    }

    partial void OnIsOpenChanged(bool value)
    {
        ToggleText = value ? "›" : "‹";
        ToggleToolTip = value ? "Rapor alanını kapat" : "Rapor alanını aç";
    }
}
