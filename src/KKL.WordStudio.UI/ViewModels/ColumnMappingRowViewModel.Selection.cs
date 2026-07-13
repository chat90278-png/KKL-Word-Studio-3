namespace KKL.WordStudio.UI.ViewModels;

public sealed partial class ColumnMappingRowViewModel
{
    private bool _isIncluded = true;

    public bool IsIncluded
    {
        get => _isIncluded;
        set => SetProperty(ref _isIncluded, value);
    }
}
