namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

public sealed class SourceFieldOptionViewModel
{
    public required string SourceField { get; init; }
    public required string DisplayText { get; init; }
}

public sealed partial class SourceFieldMatchRowViewModel : ViewModelBase
{
    public required Guid TableColumnId { get; init; }
    public required string TableColumnHeader { get; init; }
    public ObservableCollection<SourceFieldOptionViewModel> AvailableSourceFields { get; } = new();

    [ObservableProperty]
    private string? _selectedSourceField;
}
