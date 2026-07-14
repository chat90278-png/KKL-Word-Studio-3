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
    public int ErrorCount => _store.ErrorCount;
    public int WarningCount => _store.WarningCount;
    public int InformationCount => _store.InformationCount;
    public bool HasBlockingErrors => _store.HasBlockingErrors;

    public string HeaderText => TotalCount == 0
        ? "Word'e hazır — sorun bulunamadı"
        : HasBlockingErrors
            ? $"Word'e hazır değil — {ErrorCount} kritik hata düzeltilmeli"
            : WarningCount > 0
                ? $"Word'e hazır — {WarningCount} uyarı türü kontrol edilmeli"
                : $"Word'e hazır — {InformationCount} bilgi";

    public string HeaderDetailText => HasBlockingErrors
        ? "Kırmızı hatalar Word çıktısını engeller. Bir kayıttan ilgili rapor öğesine ve Excel kaynağına gidin."
        : WarningCount > 0
            ? "Sarı uyarılar çıktıyı engellemez; Word oluştururken tek bir onay istenir."
            : "Bilgi kayıtları müdahale gerektirmez ve Word çıktısını engellemez.";

    public string HeaderBackground => HasBlockingErrors ? "#FFFFE9E7" : WarningCount > 0 ? "#FFFFF8E7" : "#FFEAF7EF";
    public string HeaderBorderBrush => HasBlockingErrors ? "#FFF1AAA4" : WarningCount > 0 ? "#FFF4D68A" : "#FFA9D9BC";
    public string HeaderIcon => HasBlockingErrors ? "×" : WarningCount > 0 ? "!" : "✓";
    public string HeaderIconBackground => HasBlockingErrors ? "#FFFFD2CE" : WarningCount > 0 ? "#FFFFE8A8" : "#FFDDF3E5";
    public string HeaderIconBrush => HasBlockingErrors ? "#FFB42318" : WarningCount > 0 ? "#FF9A6700" : "#FF237A47";

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
    private Task NavigateAsync(WarningDiagnosticItemViewModel? item) => NavigateOccurrenceAsync(item, reset: true);

    [RelayCommand]
    private Task NavigateFirstAsync(WarningDiagnosticItemViewModel? item) => NavigateOccurrenceAsync(item, reset: true);

    [RelayCommand]
    private Task NavigateNextAsync(WarningDiagnosticItemViewModel? item) => NavigateOccurrenceAsync(item, reset: false);

    private async Task NavigateOccurrenceAsync(WarningDiagnosticItemViewModel? item, bool reset)
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

        var key = item.GetNavigationKey(reset);
        var excelNavigated = false;
        foreach (var source in group.Sources)
        {
            if (await _excelWorkspaceViewModel.NavigateToDiagnosticSourceAsync(source, key))
            {
                excelNavigated = true;
                break;
            }
        }

        NavigationStatusText = (previewNavigated, excelNavigated, key) switch
        {
            (true, true, not null) => $"İlgili rapor öğesi ve Excel kaydı vurgulandı. Örnek anahtar: {key}",
            (true, true, null) => "İlgili rapor öğesi ve Excel kaynağı vurgulandı.",
            (true, false, _) => "İlgili rapor öğesine gidildi; kaynak hücre bulunamadı.",
            (false, true, _) => "Excel kaynağına gidildi.",
            _ => "Bu kayıt için doğrudan gidilebilecek bir hedef yok."
        };
    }

    private void Diagnostics_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private void Diagnostics_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PreviewDiagnosticsStore.Count)
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
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(InformationCount));
        OnPropertyChanged(nameof(HasBlockingErrors));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(HeaderDetailText));
        OnPropertyChanged(nameof(HeaderBackground));
        OnPropertyChanged(nameof(HeaderBorderBrush));
        OnPropertyChanged(nameof(HeaderIcon));
        OnPropertyChanged(nameof(HeaderIconBackground));
        OnPropertyChanged(nameof(HeaderIconBrush));
    }
}

public sealed class WarningDiagnosticItemViewModel
{
    private int _navigationIndex = -1;

    public WarningDiagnosticItemViewModel(PreviewDiagnosticGroup group)
    {
        Group = group;
        SourceText = BuildSourceText(group);
        ExampleText = BuildExampleText(group);
    }

    public PreviewDiagnosticGroup Group { get; }
    public string CodeText => string.Equals(Group.Code, PreviewDiagnosticCodes.Unclassified, StringComparison.Ordinal)
        ? "GENEL"
        : Group.Code;
    public string Title => Group.Title;
    public string Message => Group.Message;
    public string ElementName => Group.ElementName ?? string.Empty;
    public string AffectedColumnText => string.IsNullOrWhiteSpace(Group.AffectedColumn)
        ? string.Empty
        : $"Etkilenen sütun: {Group.AffectedColumn}";
    public string AffectedRowsText => Group.RowNumbers.Count > 0
        ? $"Örnek satırlar: {string.Join(", ", Group.RowNumbers.Take(5))}{(Group.RowNumbers.Count > 5 ? ", …" : string.Empty)}"
        : Group.OccurrenceCount > 1
            ? $"Etkilenen kayıt: {Group.OccurrenceCount}"
            : string.Empty;
    public string DistinctKeyText => Group.KeyValues.Count switch
    {
        0 => string.Empty,
        1 => "1 farklı anahtar",
        > 25 => "25+ farklı anahtar",
        _ => $"{Group.KeyValues.Count} farklı anahtar"
    };
    public string ExampleText { get; }
    public string SourceText { get; }
    public int OccurrenceCount => Group.OccurrenceCount;
    public bool HasMultipleOccurrences => OccurrenceCount > 1;
    public string OccurrenceText => OccurrenceCount == 1 ? "1 kayıt" : $"{OccurrenceCount} kayıt";
    public bool HasElementName => !string.IsNullOrWhiteSpace(ElementName);
    public bool HasAffectedColumn => !string.IsNullOrWhiteSpace(Group.AffectedColumn);
    public bool HasAffectedRows => !string.IsNullOrWhiteSpace(AffectedRowsText);
    public bool HasDistinctKeys => Group.KeyValues.Count > 0;
    public bool HasExamples => !string.IsNullOrWhiteSpace(ExampleText);
    public bool HasSource => Group.Sources.Count > 0;
    public bool CanNavigate => Group.ElementId is not null || HasSource;
    public bool CanNavigateNext => Group.KeyValues.Count > 1;
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

    public string? GetNavigationKey(bool reset)
    {
        if (Group.KeyValues.Count == 0)
            return null;

        _navigationIndex = reset
            ? 0
            : (_navigationIndex + 1 + Group.KeyValues.Count) % Group.KeyValues.Count;
        return Group.KeyValues[_navigationIndex];
    }

    private static string BuildExampleText(PreviewDiagnosticGroup group)
    {
        if (group.KeyValues.Count == 0)
            return string.Empty;

        var examples = group.KeyValues.Take(3).Select(value => $"• Anahtar: {value}");
        return "Örnekler:\n" + string.Join("\n", examples);
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
