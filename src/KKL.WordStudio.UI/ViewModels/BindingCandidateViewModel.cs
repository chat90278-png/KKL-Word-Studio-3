namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>A single worksheet shown as a candidate bind target in the Change Binding view — "Aircraft.xlsx" broken down into its individual worksheets, each independently bindable (see ADR 0009).</summary>
public sealed partial class BindingCandidateViewModel : ViewModelBase
{
    public required string DataSourceName { get; init; }
    public required string WorksheetName { get; init; }
    public required string RangeText { get; init; }
    public required bool IsConfigured { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
