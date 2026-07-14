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
    public string HeaderText => !HasItems
        ? "Rapor Word'e hazır"
        : $"{_store.ErrorCount} hata · {_store.WarningCount} uyarı · {_store.InformationCount} bilgi";
    public string DetailText => !HasItems
        ? "Önizleme doğrulamasında sorun bulunmadı."
        : $"{Count} grup içinde {_store.TotalOccurrenceCount} bulgu birleştirildi.";

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
            ReportPaneViewModel.Shared.OpenForAction();
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
            (true, true) => "Önizleme öğesi ve Excel kaynağı vurgulandı.",
            (true, false) => "Önizleme öğesine gidildi; belirli kaynak hücresi bulunamadı.",
            (false, true) => "Excel kaynağına gidildi.",
            _ => "Bu bulgu için doğrudan gidilebilecek bir hedef yok."
        };
    }

    private void Diagnostics_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private void Diagnostics_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PreviewDiagnosticsStore.Count)
            or nameof(PreviewDiagnosticsStore.TotalOccurrenceCount)
            or nameof(PreviewDiagnosticsStore.ErrorCount)
            or nameof(PreviewDiagnosticsStore.WarningCount)
            or nameof(PreviewDiagnosticsStore.InformationCount)
            or nameof(PreviewDiagnosticsStore.HasItems))
        {
            PublishSummaryProperties();
        }
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
        OnPropertyChanged(nameof(DetailText));
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
    public PreviewDiagnosticSeverity Severity => Diagnostic.Severity;
    public string SeverityText => Severity switch
    {
        PreviewDiagnosticSeverity.Error => "Hata",
        PreviewDiagnosticSeverity.Warning => "Uyarı",
        _ => "Bilgi"
    };
    public string SeverityAccent => Severity switch
    {
        PreviewDiagnosticSeverity.Error => "#FFD92D20",
        PreviewDiagnosticSeverity.Warning => "#FFF2B84B",
        _ => "#FF3B82F6"
    };
    public string SeverityBackground => Severity switch
    {
        PreviewDiagnosticSeverity.Error => "#FFFFE8E7",
        PreviewDiagnosticSeverity.Warning => "#FFFFF4D6",
        _ => "#FFEAF2FF"
    };
    public string OccurrenceText => Diagnostic.OccurrenceCount > 1
        ? $"{Diagnostic.OccurrenceCount} kez"
        : string.Empty;
    public bool HasMultipleOccurrences => Diagnostic.OccurrenceCount > 1;
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
