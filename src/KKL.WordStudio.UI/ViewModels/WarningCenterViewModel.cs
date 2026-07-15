namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Preview;

public enum WarningCenterFilter
{
    All,
    Error,
    Warning,
    Information
}

public sealed partial class WarningCenterViewModel : ViewModelBase
{
    private readonly PreviewDiagnosticsStore _store;
    private readonly PreviewViewModel _previewViewModel;
    private readonly ExcelWorkspaceViewModel _excelWorkspaceViewModel;

    public ObservableCollection<WarningDiagnosticItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private string _navigationStatusText = string.Empty;

    [ObservableProperty]
    private WarningCenterFilter _filter = WarningCenterFilter.All;

    public int Count => Items.Count;
    public bool HasItems => Count > 0;
    public int TotalCount => _store.Count;
    public int FindingCount => _store.FindingCount;
    public int ErrorCount => _store.ErrorCount;
    public int WarningCount => _store.WarningCount;
    public int InformationCount => _store.InformationCount;
    public bool HasBlockingErrors => _store.HasBlockingErrors;
    public string HeaderText => TotalCount == 0
        ? "Sorun bulunamadı"
        : $"{TotalCount} sorun türü · {FindingCount} açık bulgu";

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
    private void ShowAll() => Filter = WarningCenterFilter.All;

    [RelayCommand]
    private void ShowErrors() => Filter = WarningCenterFilter.Error;

    [RelayCommand]
    private void ShowWarnings() => Filter = WarningCenterFilter.Warning;

    [RelayCommand]
    private void ShowInformation() => Filter = WarningCenterFilter.Information;

    partial void OnFilterChanged(WarningCenterFilter value) => RebuildItems();

    [RelayCommand]
    private async Task NavigateAsync(WarningDiagnosticItemViewModel? item)
    {
        if (item is null)
            return;

        var group = item.Group;
        var previewNavigated = false;
        if (group.ElementId is { } elementId)
        {
            ReportPaneViewModel.Shared.OpenForAction();
            _previewViewModel.NavigateToElement(elementId);
            previewNavigated = true;
        }

        var excelNavigated = false;
        foreach (var source in group.Sources)
        {
            if (await _excelWorkspaceViewModel.NavigateToDiagnosticSourceAsync(
                    source,
                    group.KeyValues,
                    group.AffectedColumn))
            {
                excelNavigated = true;
                break;
            }
        }

        NavigationStatusText = (previewNavigated, excelNavigated) switch
        {
            (true, true) => "İlgili rapor öğesi ve sorunlu Excel hücresi vurgulandı.",
            (true, false) => "İlgili rapor öğesine gidildi; sorunlu kaynak hücresi bulunamadı.",
            (false, true) => "Sorunlu Excel hücresine gidildi.",
            _ => "Bu kayıt için doğrudan gidilebilecek bir hedef yok."
        };
    }

    private void Diagnostics_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private void Diagnostics_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PreviewDiagnosticsStore.Count)
            or nameof(PreviewDiagnosticsStore.FindingCount)
            or nameof(PreviewDiagnosticsStore.ErrorCount)
            or nameof(PreviewDiagnosticsStore.WarningCount)
            or nameof(PreviewDiagnosticsStore.InformationCount)
            or nameof(PreviewDiagnosticsStore.HasBlockingErrors))
        {
            RebuildItems();
        }
    }

    private void RebuildItems()
    {
        var selectedSeverity = Filter switch
        {
            WarningCenterFilter.Error => PreviewDiagnosticSeverity.Error,
            WarningCenterFilter.Warning => PreviewDiagnosticSeverity.Warning,
            WarningCenterFilter.Information => PreviewDiagnosticSeverity.Information,
            _ => (PreviewDiagnosticSeverity?)null
        };

        Items.Clear();
        foreach (var group in _store.Groups.Where(group => selectedSeverity is null || group.Severity == selectedSeverity))
            Items.Add(new WarningDiagnosticItemViewModel(group));

        NavigationStatusText = string.Empty;
        PublishSummaryProperties();
    }

    private void PublishSummaryProperties()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(FindingCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(InformationCount));
        OnPropertyChanged(nameof(HasBlockingErrors));
        OnPropertyChanged(nameof(HeaderText));
    }
}

public sealed class WarningDiagnosticItemViewModel
{
    public WarningDiagnosticItemViewModel(PreviewDiagnosticGroup group)
    {
        Group = group;
        SourceText = BuildSourceText(group);
        KeyText = BuildKeyText(group);
    }

    public PreviewDiagnosticGroup Group { get; }
    public string Title => Group.Title;
    public string Message => Group.Message;
    public string ElementName => Group.ElementName ?? string.Empty;
    public string KeyText { get; }
    public string SourceText { get; }
    public int OccurrenceCount => Group.OccurrenceCount;
    public bool HasMultipleOccurrences => OccurrenceCount > 1;
    public string OccurrenceText => $"{OccurrenceCount} açık bulgu";
    public bool HasElementName => !string.IsNullOrWhiteSpace(ElementName);
    public bool HasKey => Group.DistinctKeyCount > 0;
    public bool HasSource => Group.Sources.Count > 0;
    public bool CanNavigate => Group.ElementId is not null || HasSource;
    public bool IsError => Group.Severity == PreviewDiagnosticSeverity.Error;
    public bool IsWarning => Group.Severity == PreviewDiagnosticSeverity.Warning;
    public bool IsInformation => Group.Severity == PreviewDiagnosticSeverity.Information;
    public string SeverityText => Group.Severity switch
    {
        PreviewDiagnosticSeverity.Error => "Hata",
        PreviewDiagnosticSeverity.Warning => "Uyarı",
        _ => "Bilgi"
    };
    public string SeverityBrush => Group.Severity switch
    {
        PreviewDiagnosticSeverity.Error => "#FFD9485F",
        PreviewDiagnosticSeverity.Warning => "#FFF2B84B",
        _ => "#FF4B8FE2"
    };

    private static string BuildKeyText(PreviewDiagnosticGroup group)
    {
        if (group.DistinctKeyCount == 0)
            return string.Empty;
        if (group.DistinctKeyCount == 1)
            return $"Anahtar: {group.KeyValues.FirstOrDefault()}";
        return $"{group.DistinctKeyCount} farklı anahtar";
    }

    private static string BuildSourceText(PreviewDiagnosticGroup group)
    {
        if (group.Sources.Count == 0)
            return string.Empty;

        var visible = group.Sources.Take(3).Select(source =>
        {
            var parts = new[] { source.DataSourceName, source.WorksheetName, source.RangeReference }
                .Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" · ", parts);
        }).ToList();

        if (group.Sources.Count > visible.Count)
            visible.Add($"+{group.Sources.Count - visible.Count} kaynak");

        return string.Join("  •  ", visible);
    }
}
