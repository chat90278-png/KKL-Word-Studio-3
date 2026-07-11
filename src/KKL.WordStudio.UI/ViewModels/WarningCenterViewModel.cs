namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Preview;

public sealed partial class WarningCenterViewModel : ViewModelBase
{
    private readonly PreviewDiagnosticsStore _store;
    private readonly PreviewViewModel _previewViewModel;
    private readonly ExcelWorkspaceViewModel _excelWorkspaceViewModel;

    public ObservableCollection<WarningDiagnosticItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private string _navigationStatusText = string.Empty;

    public int Count => Items.Count;
    public bool HasItems => Count > 0;
    public string HeaderText => HasItems ? $"{Count} uyarı bulundu" : "Uyarı yok";

    public WarningCenterViewModel(
        PreviewDiagnosticsStore store,
        PreviewViewModel previewViewModel,
        ExcelWorkspaceViewModel excelWorkspaceViewModel)
    {
        _store = store;
        _previewViewModel = previewViewModel;
        _excelWorkspaceViewModel = excelWorkspaceViewModel;

        _store.Items.CollectionChanged += Diagnostics_CollectionChanged;
        _store.PropertyChanged += Diagnostics_PropertyChanged;
        RebuildItems();
    }

    [RelayCommand]
    private async Task NavigateAsync(WarningDiagnosticItemViewModel? item)
    {
        if (item is null)
            return;

        var diagnostic = item.Diagnostic;
        var previewNavigated = false;
        if (diagnostic.ElementId is { } elementId)
        {
            _previewViewModel.NavigateToElement(elementId);
            previewNavigated = true;
        }

        var excelNavigated = false;
        foreach (var source in diagnostic.Sources)
        {
            if (await _excelWorkspaceViewModel.NavigateToDiagnosticSourceAsync(source, diagnostic.KeyValue))
            {
                excelNavigated = true;
                break;
            }
        }

        NavigationStatusText = (previewNavigated, excelNavigated) switch
        {
            (true, true) => "Önizleme tablosu ve Excel kaynağı vurgulandı.",
            (true, false) => "Önizleme tablosuna gidildi; kaynak hücre bulunamadı.",
            (false, true) => "Excel kaynağına gidildi.",
            _ => "Bu uyarı için doğrudan gidilebilecek bir hedef yok."
        };
    }

    private void Diagnostics_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private void Diagnostics_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PreviewDiagnosticsStore.Count) or nameof(PreviewDiagnosticsStore.HasItems))
            PublishSummaryProperties();
    }

    private void RebuildItems()
    {
        Items.Clear();
        foreach (var diagnostic in _store.Items)
            Items.Add(new WarningDiagnosticItemViewModel(diagnostic));

        NavigationStatusText = string.Empty;
        PublishSummaryProperties();
    }

    private void PublishSummaryProperties()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(HeaderText));
    }
}

public sealed class WarningDiagnosticItemViewModel
{
    public WarningDiagnosticItemViewModel(PreviewDiagnostic diagnostic)
    {
        Diagnostic = diagnostic;
        SourceText = BuildSourceText(diagnostic);
    }

    public PreviewDiagnostic Diagnostic { get; }
    public string Title => Diagnostic.Title;
    public string Message => Diagnostic.Message;
    public string ElementName => Diagnostic.ElementName ?? string.Empty;
    public string KeyText => string.IsNullOrWhiteSpace(Diagnostic.KeyValue) ? string.Empty : $"Anahtar: {Diagnostic.KeyValue}";
    public string SourceText { get; }
    public bool HasElementName => !string.IsNullOrWhiteSpace(ElementName);
    public bool HasKey => !string.IsNullOrWhiteSpace(Diagnostic.KeyValue);
    public bool HasSource => Diagnostic.Sources.Count > 0;
    public bool CanNavigate => Diagnostic.ElementId is not null || HasSource;

    private static string BuildSourceText(PreviewDiagnostic diagnostic)
    {
        if (diagnostic.Sources.Count == 0)
            return string.Empty;

        return string.Join("  •  ", diagnostic.Sources.Select(source =>
        {
            var parts = new[] { source.DataSourceName, source.WorksheetName, source.RangeReference }
                .Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" · ", parts);
        }));
    }
}
