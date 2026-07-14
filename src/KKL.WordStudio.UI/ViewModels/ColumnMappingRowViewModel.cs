namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using KKL.WordStudio.Application.Excel;

/// <summary>
/// One source-grid column option. Source order and identity stay tied to Excel;
/// IsIncluded and SemanticRole only control the standard Word-table projection.
/// </summary>
public sealed partial class ColumnMappingRowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _sourceColumn = string.Empty;

    /// <summary>Stable provider field used by working-data/report binding.</summary>
    [ObservableProperty]
    private string _providerField = string.Empty;

    /// <summary>User-visible/edited header text used in Preview and Word.</summary>
    [ObservableProperty]
    private string _fieldName = string.Empty;

    [ObservableProperty]
    private string _dataType = "string";

    [ObservableProperty]
    private bool _isIncluded;

    [ObservableProperty]
    private ExcelSemanticFieldRole _semanticRole = ExcelSemanticFieldRole.Unknown;

    [ObservableProperty]
    private int _sourceOrder;

    public bool IsCanonical => SemanticRole != ExcelSemanticFieldRole.Unknown;
}
