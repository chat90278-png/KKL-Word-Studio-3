namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// A table block on the report design surface. Reworked in Sprint 7 from a
/// bound DataTable/DataGrid into explicit column/row ViewModels so the
/// DISPLAYED column headers are inline-editable (double-click) while the
/// data cells stay strictly read-only — the report surface owns structure
/// and display headers, never the Excel-derived data itself.
/// </summary>
public sealed partial class TablePreviewBlockViewModel : PreviewBlockViewModel
{
    public required string Name { get; init; }
    public string? Caption { get; init; }
    public string? DataSourceName { get; init; }
    public int SourceCount { get; init; }
    public string? SourceError { get; init; }
    public bool HasSourceError => !string.IsNullOrWhiteSpace(SourceError);
    public bool FilterWasIgnored { get; init; }

    [ObservableProperty]
    private bool _isCaptionEditing;

    [ObservableProperty]
    private string _captionEditText = string.Empty;

    public required IReadOnlyList<PreviewTableColumnViewModel> Columns { get; init; }
    public required IReadOnlyList<PreviewTableRowViewModel> Rows { get; init; }

    public string CaptionDisplayText => string.IsNullOrWhiteSpace(Caption)
        ? "Tablo başlığı eklemek için çift tıklayın"
        : Caption;

    public bool HasCaption => !string.IsNullOrWhiteSpace(Caption);

    public string BindingSummary => SourceCount > 1
        ? $"{Name} — {SourceCount} veri kaynağı{(FilterWasIgnored ? " (filtre henüz uygulanmıyor)" : string.Empty)}"
        : DataSourceName is not null
            ? $"{Name} — '{DataSourceName}' kaynağına bağlı{(FilterWasIgnored ? " (filtre henüz uygulanmıyor)" : string.Empty)}"
            : $"{Name} — bağlı değil";
}

/// <summary>One displayed table column header — inline-editable from the design surface. Index + owning table Id are what the commit needs to rename the real TableColumn.Header.</summary>
public sealed partial class PreviewTableColumnViewModel : ViewModelBase
{
    public required Guid TableElementId { get; init; }
    public required int Index { get; init; }

    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;
}

/// <summary>One rendered data row — deliberately plain strings with no edit state: source data cells are read-only on the report design surface (Sprint 7 ownership rule).</summary>
public sealed class PreviewTableRowViewModel
{
    public required IReadOnlyList<string> Cells { get; init; }
}
