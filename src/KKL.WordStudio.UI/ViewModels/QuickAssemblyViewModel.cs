namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.QuickAssembly;

public sealed partial class QuickAssemblyViewModel : ViewModelBase
{
    private readonly ExcelWorkspaceViewModel _excelWorkspace;
    private readonly QuickAssemblySelection _selection = new();
    private readonly QuickAssemblyBatchOrchestrator _orchestrator = new();
    private readonly HashSet<OpenWorkbookViewModel> _subscribedWorkbooks = [];
    private CancellationTokenSource? _transferCancellation;

    public ExcelWorkspaceViewModel ExcelWorkspace => _excelWorkspace;
    public ObservableCollection<QuickAssemblyWorkbookItemViewModel> Sources { get; } = new();

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Yüklü Excel sayfalarını seçerek tek işlemde rapora ekleyin.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    private int _completedCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    private int _totalCount;

    [ObservableProperty]
    private string _currentItemText = string.Empty;

    public bool HasSources => Sources.Count > 0;
    public int SelectedCount => Sources.Sum(source => source.SelectedCount);
    public double ProgressPercent => TotalCount <= 0 ? 0 : CompletedCount * 100d / TotalCount;

    public QuickAssemblyViewModel(ExcelWorkspaceViewModel excelWorkspace)
    {
        _excelWorkspace = excelWorkspace;
        _excelWorkspace.OpenWorkbooks.CollectionChanged += OpenWorkbooks_CollectionChanged;
        RefreshWorkbookSubscriptions();
        SynchronizeSources();
    }

    [RelayCommand]
    private void TogglePanel()
    {
        SynchronizeSources();
        IsOpen = !IsOpen;
    }

    [RelayCommand]
    private void ClosePanel() => IsOpen = false;

    private bool CanModifySelection() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanModifySelection))]
    private void SelectAll()
    {
        foreach (var source in Sources)
            source.IsSelected = true;
        RefreshSelectionState();
    }

    [RelayCommand(CanExecute = nameof(CanModifySelection))]
    private void ClearSelection()
    {
        foreach (var source in Sources)
            source.IsSelected = false;
        RefreshSelectionState();
    }

    private bool CanTransferSelected() => !IsBusy && SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanTransferSelected))]
    private async Task TransferSelectedAsync()
    {
        if (IsBusy)
            return;

        var selectedTargets = _selection.SelectedTargets.ToList();
        if (selectedTargets.Count == 0)
        {
            StatusText = "Rapora aktarılacak en az bir sayfa seçin.";
            return;
        }

        _transferCancellation?.Dispose();
        using var transferCancellation = new CancellationTokenSource();
        _transferCancellation = transferCancellation;
        CompletedCount = 0;
        TotalCount = selectedTargets.Count;
        CurrentItemText = "Toplu aktarım hazırlanıyor…";
        IsBusy = true;
        RefreshCommandStates();
        StatusText = $"{selectedTargets.Count} sayfa rapora aktarılıyor…";

        try
        {
            var progress = new InlineProgress<QuickAssemblyProgress>(ApplyProgress);
            var result = await _orchestrator.ExecuteAsync(
                selectedTargets,
                _excelWorkspace.TransferQuickAssemblyTargetAsync,
                transferCancellation.Token,
                progress);

            ApplyTargetResults(result);
            StatusText = result.IsCancelled
                ? $"İptal edildi · {result.CreatedCount} tablo oluşturuldu · {result.SkippedCount} atlandı · {result.FailedCount} başarısız"
                : $"{result.CreatedCount} tablo oluşturuldu · {result.SkippedCount} atlandı · {result.FailedCount} başarısız";
            CurrentItemText = result.IsCancelled
                ? $"{result.Targets.Count}/{result.TotalTargetCount} hedef tamamlandı; kalanlar seçili bırakıldı."
                : $"{result.TotalTargetCount}/{result.TotalTargetCount} hedef tamamlandı.";
        }
        catch (Exception exception)
        {
            StatusText = $"Toplu aktarım başlatılamadı · {exception.Message}";
            CurrentItemText = "Tamamlanan hedefler korunur; başarısız hedefleri yeniden deneyin.";
        }
        finally
        {
            if (ReferenceEquals(_transferCancellation, transferCancellation))
                _transferCancellation = null;
            IsBusy = false;
            RefreshSelectionState();
            RefreshCommandStates();
        }
    }

    private bool CanCancelTransferSelected() =>
        IsBusy && _transferCancellation is { IsCancellationRequested: false };

    [RelayCommand(CanExecute = nameof(CanCancelTransferSelected))]
    private void CancelTransferSelected()
    {
        if (_transferCancellation is null || _transferCancellation.IsCancellationRequested)
            return;

        _transferCancellation.Cancel();
        StatusText = $"İptal isteği alındı · {CompletedCount}/{TotalCount} hedef tamamlandı";
        CurrentItemText = "Çalışan hedef güvenli iptal noktasında durduruluyor…";
        CancelTransferSelectedCommand.NotifyCanExecuteChanged();
    }

    private void ApplyProgress(QuickAssemblyProgress progress)
    {
        CompletedCount = progress.CompletedCount;
        TotalCount = progress.TotalCount;

        if (progress.IsCancelled)
        {
            CurrentItemText = $"{progress.CompletedCount}/{progress.TotalCount} hedef tamamlandı · iptal edildi";
            return;
        }

        if (progress.CurrentTarget is null)
        {
            CurrentItemText = progress.TotalCount == 0
                ? "Aktarılacak hedef yok."
                : $"0/{progress.TotalCount} hedef · başlanıyor…";
            return;
        }

        var targetName = $"{progress.CurrentTarget.WorkbookDisplayName} / {progress.CurrentTarget.WorksheetName}";
        CurrentItemText = progress.LastStatus is null
            ? $"{targetName} işleniyor…"
            : $"{targetName} tamamlandı · {progress.CompletedCount}/{progress.TotalCount}";
    }

    private void ApplyTargetResults(QuickAssemblyBatchResult result)
    {
        foreach (var item in result.Targets)
        {
            var sheet = Sources
                .SelectMany(source => source.Sheets)
                .FirstOrDefault(candidate => string.Equals(candidate.Key, item.Target.Key, StringComparison.OrdinalIgnoreCase));
            if (sheet is null)
                continue;

            sheet.LastResultText = item.Status switch
            {
                QuickAssemblyTransferStatus.Created => "Oluşturuldu",
                QuickAssemblyTransferStatus.Skipped => $"Atlandı · {item.Message}",
                _ => $"Başarısız · {item.Message}"
            };

            // Successful targets are deselected so a second click cannot
            // accidentally create duplicate report tables. Failed/skipped and
            // not-yet-started cancelled targets remain selected for retry.
            if (item.Status == QuickAssemblyTransferStatus.Created)
                sheet.IsSelected = false;
        }
    }

    partial void OnIsBusyChanged(bool value) => RefreshCommandStates();

    private void RefreshCommandStates()
    {
        TransferSelectedCommand.NotifyCanExecuteChanged();
        CancelTransferSelectedCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        ClearSelectionCommand.NotifyCanExecuteChanged();
    }

    private void OpenWorkbooks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshWorkbookSubscriptions();
        SynchronizeSources();
    }

    private void SheetNames_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => SynchronizeSources();

    private void RefreshWorkbookSubscriptions()
    {
        var current = _excelWorkspace.OpenWorkbooks.ToHashSet();
        foreach (var stale in _subscribedWorkbooks.Where(workbook => !current.Contains(workbook)).ToList())
        {
            stale.SheetNames.CollectionChanged -= SheetNames_CollectionChanged;
            _subscribedWorkbooks.Remove(stale);
        }

        foreach (var workbook in current.Where(workbook => !_subscribedWorkbooks.Contains(workbook)))
        {
            workbook.SheetNames.CollectionChanged += SheetNames_CollectionChanged;
            _subscribedWorkbooks.Add(workbook);
        }
    }

    private void SynchronizeSources()
    {
        _selection.Synchronize(_excelWorkspace.OpenWorkbooks.Select(workbook => new QuickAssemblySourceSnapshot
        {
            SourcePath = workbook.FilePath,
            DisplayName = workbook.DisplayName,
            WorksheetNames = workbook.SheetNames.ToList()
        }));

        Sources.Clear();
        foreach (var group in _selection.Targets
                     .GroupBy(target => new { target.SourcePath, target.WorkbookDisplayName, target.WorkbookOrder })
                     .OrderBy(group => group.Key.WorkbookOrder))
        {
            Sources.Add(new QuickAssemblyWorkbookItemViewModel(
                group.Key.SourcePath,
                group.Key.WorkbookDisplayName,
                group.OrderBy(target => target.WorksheetOrder).ToList(),
                RefreshSelectionState));
        }

        OnPropertyChanged(nameof(HasSources));
        RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        OnPropertyChanged(nameof(SelectedCount));
        foreach (var source in Sources)
            source.RefreshAggregateSelection();
        TransferSelectedCommand.NotifyCanExecuteChanged();
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

public sealed class QuickAssemblyWorkbookItemViewModel : ViewModelBase
{
    private readonly Action _selectionChanged;
    private bool _updatingChildren;

    public string SourcePath { get; }
    public string DisplayName { get; }
    public ObservableCollection<QuickAssemblySheetItemViewModel> Sheets { get; } = new();
    public int SelectedCount => Sheets.Count(sheet => sheet.IsSelected);

    public bool IsSelected
    {
        get => Sheets.Count > 0 && Sheets.All(sheet => sheet.IsSelected);
        set
        {
            if (_updatingChildren)
                return;

            _updatingChildren = true;
            try
            {
                foreach (var sheet in Sheets)
                    sheet.IsSelected = value;
            }
            finally
            {
                _updatingChildren = false;
            }

            RefreshAggregateSelection();
            _selectionChanged();
        }
    }

    public QuickAssemblyWorkbookItemViewModel(
        string sourcePath,
        string displayName,
        IReadOnlyList<QuickAssemblyTarget> targets,
        Action selectionChanged)
    {
        SourcePath = sourcePath;
        DisplayName = displayName;
        _selectionChanged = selectionChanged;

        foreach (var target in targets)
        {
            Sheets.Add(new QuickAssemblySheetItemViewModel(target, () =>
            {
                RefreshAggregateSelection();
                _selectionChanged();
            }));
        }
    }

    public void RefreshAggregateSelection()
    {
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(SelectedCount));
    }
}

public sealed class QuickAssemblySheetItemViewModel : ViewModelBase
{
    private readonly QuickAssemblyTarget _target;
    private readonly Action _selectionChanged;
    private bool _isSelected;
    private string _caption;
    private string? _lastResultText;

    public string Key => _target.Key;
    public string WorksheetName => _target.WorksheetName;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
                return;
            _target.IsSelected = value;
            _selectionChanged();
        }
    }

    public string Caption
    {
        get => _caption;
        set
        {
            if (!SetProperty(ref _caption, value ?? string.Empty))
                return;
            _target.Caption = string.IsNullOrWhiteSpace(_caption) ? null : _caption.Trim();
        }
    }

    public string? LastResultText
    {
        get => _lastResultText;
        set => SetProperty(ref _lastResultText, value);
    }

    public QuickAssemblySheetItemViewModel(QuickAssemblyTarget target, Action selectionChanged)
    {
        _target = target;
        _selectionChanged = selectionChanged;
        _isSelected = target.IsSelected;
        _caption = target.Caption ?? string.Empty;
    }
}
