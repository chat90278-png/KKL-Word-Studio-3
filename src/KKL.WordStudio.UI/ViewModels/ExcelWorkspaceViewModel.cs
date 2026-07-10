namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Importing;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.UI.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// Drives the Excel Workspace flow: open Excel file(s) → pick sheet →
/// automatically configure the likely tabular range → optionally prepare/map
/// project-owned working data → transfer to the report.
///
/// Sprint 7 makes "Word'e Aktar" the PRIMARY action of this panel: it
/// transfers the CURRENT active workbook/worksheet/range directly into the
/// report design in one step, using the shared selection to decide the
/// target (selected table / new table under a selected heading / default
/// insertion). Column mapping stays available in the drawer, but it's an
/// optional advanced feature — never a prerequisite.
///
/// Sprint 9 keeps the original .xlsx/.xlsm source read-only and creates a
/// project-owned WorksheetWorkingData snapshot lazily on the first mutation.
/// Preview/Word continue to resolve rows through IDataProvider.
///
/// Deliberately holds the (potentially large) preview grid itself — see
/// ADR 0004: Workspace (Application layer) only tracks lightweight
/// identifiers/flags, so bulk preview data belongs here, in the panel that
/// actually renders it, not in the cross-cutting session singleton.
/// </summary>
public sealed partial class ExcelWorkspaceViewModel : ViewModelBase
{
    private readonly IExcelWorkbookReader _excelReader;
    private readonly IExcelDataRangeDetector _dataRangeDetector;
    private readonly IWorkspace _workspace;
    private readonly IFileDialogService _fileDialogService;
    private readonly IExcelReportTransferService _transferService;
    private readonly IWorksheetWorkingDataService _workingDataService;
    private readonly IWorkingDataHistoryRegistry _historyRegistry;
    private readonly IDialogService _dialogService;
    private readonly ILogger<ExcelWorkspaceViewModel> _logger;

    private SheetPreview? _currentPreview;

    /// <summary>
    /// Preparation-only view state (row filter + column visibility) per
    /// worksheet instance. Runtime-only, never persisted, never affects
    /// Preview/Word — see <see cref="WorkingDataViewState"/>.
    /// </summary>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Worksheet, WorkingDataViewState> _viewStates = new();

    /// <summary>Working-data row indexes currently matched by Find (case-insensitive), and the cursor within them.</summary>
    private IReadOnlyList<WorkingDataCell> _findMatches = Array.Empty<WorkingDataCell>();
    private int _findCursor = -1;

    /// <summary>
    /// True only after the user explicitly clicked "Eşlemeyi Uygula" in the
    /// mapping drawer for the CURRENT preview. Only applied mappings ride
    /// along with a transfer — merely opening the drawer (which auto-suggests
    /// rows) and cancelling must not silently turn a casual transfer into a
    /// mapped one.
    /// </summary>
    private bool _mappingsApplied;

    public ObservableCollection<OpenWorkbookViewModel> OpenWorkbooks { get; } = new();
    public ObservableCollection<ColumnMappingRowViewModel> ColumnMappings { get; } = new();
    public ObservableCollection<SourceFieldMatchRowViewModel> SourceFieldMappingRows { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransferToReportCommand))]
    private OpenWorkbookViewModel? _selectedWorkbook;

    [ObservableProperty]
    private DataTable _previewTable = new();

    [ObservableProperty]
    private int _startRow = 1;

    [ObservableProperty]
    private bool _startRowIsHeader = true;

    [ObservableProperty]
    private int _configuredDataStartRow = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TransferToReportCommand))]
    private int? _detectedDataEndRow;

    [ObservableProperty]
    private bool _wasAutoDetected;

    [ObservableProperty]
    private int _configuredStartColumn = 1;

    [ObservableProperty]
    private int _configuredEndColumn = 1;

    [ObservableProperty]
    private bool _rangeRequiresReview;

    /// <summary>Runtime-only display provenance for the current source candidate; persisted ranges render as configured.</summary>
    [ObservableProperty]
    private bool _rangeIsAutomaticCandidate;

    [ObservableProperty]
    private bool _isRangeEditorOpen;

    [ObservableProperty]
    private bool _editorHasHeader = true;

    [ObservableProperty]
    private int? _editorHeaderRow = 1;

    [ObservableProperty]
    private int _editorDataStartRow = 2;

    [ObservableProperty]
    private int _editorDataEndRow = 2;

    [ObservableProperty]
    private string _editorStartColumn = "A";

    [ObservableProperty]
    private string _editorEndColumn = "A";

    [ObservableProperty]
    private string _dataSourceName = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isMappingDrawerOpen;

    [ObservableProperty]
    private bool _isExcelDropActive;

    public bool HasOpenWorkbook => SelectedWorkbook is not null;
    public bool HasWorkingData => GetCurrentWorksheet()?.WorkingData is not null;
    public bool IsSourceMissing => SelectedWorkbook is not null && !System.IO.File.Exists(SelectedWorkbook.FilePath);
    public bool CanResetWorkingData => HasWorkingData && !IsSourceMissing;
    public string WorkingDataStateText => IsSourceMissing
        ? "Kaynak Excel bulunamadı"
        : HasWorkingData ? "Değiştirildi" : "Kaynak veri";

    /// <summary>True while the "this table is already configured" decision flyout is open (see TransferToReport).</summary>
    [ObservableProperty]
    private bool _isTransferChoiceOpen;

    /// <summary>Name of the configured table awaiting the user's transfer decision — shown in the flyout.</summary>
    [ObservableProperty]
    private string _transferChoiceTableName = string.Empty;

    [ObservableProperty]
    private bool _isSourceFieldMappingOpen;

    [ObservableProperty]
    private string _sourceFieldMappingTableName = string.Empty;

    /// <summary>The Excel row currently acting as the header, or null if the range has no header row — drives the grid's header-row highlight.</summary>
    public int? HeaderRowNumber => StartRowIsHeader ? StartRow : null;

    /// <summary>The first row actually treated as data — drives the grid's "data start" indicator.</summary>
    public int EffectiveDataStartRow => ConfiguredDataStartRow;

    public string RangeStatusText
    {
        get
        {
            if (DetectedDataEndRow is not { } end) return "—";
            var range = BuildCurrentRange(end);
            var state = RangeRequiresReview
                ? "Veri aralığını doğrulayın"
                : RangeIsAutomaticCandidate ? "Otomatik algılandı" : "Yapılandırıldı";
            return $"{range.RangeReference} · {state}";
        }
    }

    public ExcelWorkspaceViewModel(
        IExcelWorkbookReader excelReader,
        IExcelDataRangeDetector dataRangeDetector,
        IWorkspace workspace,
        IFileDialogService fileDialogService,
        IExcelReportTransferService transferService,
        IWorksheetWorkingDataService workingDataService,
        IWorkingDataHistoryRegistry historyRegistry,
        IDialogService dialogService,
        ILogger<ExcelWorkspaceViewModel> logger)
    {
        _excelReader = excelReader;
        _dataRangeDetector = dataRangeDetector;
        _workspace = workspace;
        _fileDialogService = fileDialogService;
        _transferService = transferService;
        _workingDataService = workingDataService;
        _historyRegistry = historyRegistry;
        _dialogService = dialogService;
        _logger = logger;

        // The transfer button's availability also depends on the active
        // report — refresh CanExecute when the shared workspace changes.
        _workspace.WorkspaceChanged += (_, _) =>
        {
            TransferToReportCommand.NotifyCanExecuteChanged();

            // Project open/new must never resurrect stale runtime history. New
            // worksheet instances already get fresh weak-keyed histories, but we
            // additionally hard-clear the registry on an actual project switch.
            if (!ReferenceEquals(_lastKnownProject, _workspace.ActiveProject))
            {
                _lastKnownProject = _workspace.ActiveProject;
                _historyRegistry.Clear();
                _findMatches = Array.Empty<WorkingDataCell>();
                _findCursor = -1;
                PublishUndoRedoState();
            }
        };
    }

    private KKL.WordStudio.Domain.Projects.Project? _lastKnownProject;

    [RelayCommand]
    private async Task OpenExcelFileAsync()
    {
        var filePath = _fileDialogService.OpenExcelFile();
        if (filePath is null) return;

        await OpenWorkbookFromPathAsync(filePath);
    }

    public void SetExcelDropActive(bool isActive) => IsExcelDropActive = isActive;

    /// <summary>
    /// WPF code-behind routes the OS drop gesture here; validation and the
    /// actual open action stay outside pixel-level UI code and reuse the same
    /// OpenWorkbookFromPathAsync workflow as the file picker/Project Explorer.
    /// </summary>
    public async Task HandleDroppedFilesAsync(IReadOnlyList<string> filePaths)
    {
        IsExcelDropActive = false;
        var decision = SourceFileDropValidator.EvaluateExcelDrop(filePaths);
        if (!decision.IsAccepted || decision.FilePath is null)
        {
            StatusText = decision.Message ?? "Excel dosyası bırakılamadı.";
            return;
        }

        var workbook = await OpenWorkbookFromPathAsync(decision.FilePath);
        if (workbook is not null && decision.Message is not null)
            StatusText = $"{StatusText} · {decision.Message}";
    }

    /// <summary>
    /// Opens a workbook by path (file-picker command and Project Explorer
    /// navigation share this) and makes it the active workbook. Returns the
    /// workbook VM, or null on failure.
    /// </summary>
    public async Task<OpenWorkbookViewModel?> OpenWorkbookFromPathAsync(string filePath, string? preferredSheetName = null)
    {
        var existing = OpenWorkbooks.FirstOrDefault(w =>
            string.Equals(w.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedWorkbook = existing;
            return existing;
        }

        var result = await _excelReader.OpenWorkbookAsync(filePath);
        var persistedSource = _workspace.ActiveProject is { } project
            ? _workingDataService.FindDataSource(project, filePath)
            : null;
        if (result.IsFailure && (persistedSource is null || persistedSource.Workbook.Worksheets.All(w => w.WorkingData is null)))
        {
            StatusText = result.Error!;
            _logger.LogWarning("Failed to open workbook {FilePath}: {Error}", filePath, result.Error);
            return null;
        }

        var workbookVm = new OpenWorkbookViewModel
        {
            FilePath = filePath,
            DisplayName = result.IsSuccess ? System.IO.Path.GetFileName(filePath) : persistedSource!.Workbook.FileName
        };
        var worksheetNames = result.IsSuccess
            ? result.Value.Worksheets.Select(worksheet => worksheet.Name)
            : persistedSource!.Workbook.Worksheets.Select(worksheet => worksheet.Name);
        foreach (var worksheetName in worksheetNames)
            workbookVm.SheetNames.Add(worksheetName);

        // Set the sheet BEFORE making the workbook selected: selecting the
        // workbook auto-loads the preview for its current sheet, so setting
        // the sheet afterwards would trigger a second, wasted preview load.
        if (workbookVm.SheetNames.Count > 0)
        {
            workbookVm.SelectedSheetName =
                preferredSheetName is not null && workbookVm.SheetNames.Contains(preferredSheetName)
                    ? preferredSheetName
                    : workbookVm.SheetNames[0];
        }

        OpenWorkbooks.Add(workbookVm);
        SelectedWorkbook = workbookVm;
        StatusText = result.IsSuccess
            ? $"'{workbookVm.DisplayName}' açıldı ({workbookVm.SheetNames.Count} sayfa)"
            : "Kaynak Excel bulunamadı · kaydedilmiş çalışma verisi kullanılabilir";
        return workbookVm;
    }

    /// <summary>Project Explorer navigation: activate the given worksheet of the given source workbook in this panel, opening the file if it isn't open yet.</summary>
    public async Task NavigateToWorksheetAsync(string? sourcePath, string? worksheetName)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            StatusText = "Bu veri kaynağının dosya yolu kayıtlı değil.";
            return;
        }

        var workbook = OpenWorkbooks.FirstOrDefault(w =>
            string.Equals(w.FilePath, sourcePath, StringComparison.OrdinalIgnoreCase));

        if (workbook is null)
        {
            workbook = await OpenWorkbookFromPathAsync(sourcePath, worksheetName);
            return; // OpenWorkbookFromPathAsync already selected the right sheet (or reported failure)
        }

        if (worksheetName is not null && workbook.SheetNames.Contains(worksheetName) && workbook.SelectedSheetName != worksheetName)
            workbook.SelectedSheetName = worksheetName;

        if (SelectedWorkbook != workbook)
            SelectedWorkbook = workbook; // triggers the preview load
        else
            await LoadPreviewAsync();
    }

    [RelayCommand]
    private async Task LoadPreviewAsync()
    {
        if (SelectedWorkbook?.SelectedSheetName is not { } sheetName) return;

        var worksheet = GetCurrentWorksheet();
        var rangeLoadAction = ExcelRangeLoadPolicy.Decide(worksheet);
        if (rangeLoadAction == ExcelRangeLoadAction.UseWorkingData && worksheet?.WorkingData is { } workingData)
        {
            ApplyWorksheetRange(worksheet);
            _currentPreview = BuildWorkingDataPreview(sheetName, workingData);
            var viewState = ViewStateFor(worksheet);
            PreviewTable = BuildWorkingDataTable(workingData, viewState);
            LoadPersistedMappings(worksheet);
            PublishWorkingDataState();
            SetWorkspaceDataSource(sheetName);
            TransferToReportCommand.NotifyCanExecuteChanged();
            return;
        }

        if (IsSourceMissing)
        {
            if (rangeLoadAction == ExcelRangeLoadAction.UsePersistedRange && worksheet is not null)
                ApplyWorksheetRange(worksheet);
            _currentPreview = null;
            PreviewTable = new DataTable();
            StatusText = "Kaynak Excel bulunamadı";
            PublishWorkingDataState();
            TransferToReportCommand.NotifyCanExecuteChanged();
            return;
        }

        var result = await _excelReader.GetSheetPreviewAsync(SelectedWorkbook.FilePath, sheetName);
        if (result.IsFailure)
        {
            StatusText = result.Error!;
            return;
        }

        _currentPreview = result.Value;
        PreviewTable = BuildPreviewTable(result.Value);
        LoadPersistedMappings(worksheet);
        SetWorkspaceDataSource(sheetName);

        StatusText = result.Value.IsTruncated
            ? $"Önizleme {result.Value.Rows.Count} satırla sınırlandı"
            : $"{result.Value.Rows.Count} satır yüklendi";

        if (rangeLoadAction == ExcelRangeLoadAction.UsePersistedRange && worksheet is not null)
            ApplyWorksheetRange(worksheet);
        else
        {
            ResetCurrentRangeCandidate(result.Value.ColumnCount);
            await AutoDetectCurrentRangeAsync();
        }

        PublishWorkingDataState();
        TransferToReportCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task DetectDataRangeAsync() => await AutoDetectCurrentRangeAsync();

    private async Task AutoDetectCurrentRangeAsync()
    {
        if (SelectedWorkbook?.SelectedSheetName is null || _currentPreview is null) return;
        if (IsSourceMissing)
        {
            StatusText = "Kaynak Excel bulunamadı; veri aralığı yeniden algılanamaz.";
            return;
        }

        var candidateResult = _dataRangeDetector.Detect(_currentPreview);
        if (candidateResult.IsFailure)
        {
            RangeRequiresReview = true;
            StatusText = candidateResult.Error!;
            OnPropertyChanged(nameof(RangeStatusText));
            return;
        }

        var candidate = candidateResult.Value;
        var endResult = await _excelReader.DetectDataRangeAsync(
            SelectedWorkbook.FilePath,
            SelectedWorkbook.SelectedSheetName,
            candidate.DataStartRow,
            candidate.StartColumn,
            candidate.EndColumn);
        if (endResult.IsFailure || endResult.Value.DataEndRow is null)
        {
            RangeRequiresReview = true;
            StatusText = endResult.IsFailure ? endResult.Error! : "Veri bitiş satırı algılanamadı.";
            return;
        }

        StartRowIsHeader = candidate.HeaderRowIndex.HasValue;
        StartRow = candidate.HeaderRowIndex ?? candidate.DataStartRow;
        ConfiguredDataStartRow = candidate.DataStartRow;
        ConfiguredStartColumn = candidate.StartColumn;
        ConfiguredEndColumn = candidate.EndColumn;
        DetectedDataEndRow = endResult.Value.DataEndRow;
        WasAutoDetected = true;
        RangeIsAutomaticCandidate = true;
        RangeRequiresReview = candidate.RequiresReview;

        StatusText = candidate.RequiresReview ? "Veri aralığını doğrulayın" : "Veri aralığı otomatik algılandı";
        OnPropertyChanged(nameof(RangeStatusText));
        TransferToReportCommand.NotifyCanExecuteChanged();
    }

    partial void OnStartRowChanged(int value)
    {
        OnPropertyChanged(nameof(HeaderRowNumber));
        OnPropertyChanged(nameof(RangeStatusText));
    }

    partial void OnStartRowIsHeaderChanged(bool value)
    {
        OnPropertyChanged(nameof(HeaderRowNumber));
        OnPropertyChanged(nameof(RangeStatusText));
    }

    partial void OnConfiguredDataStartRowChanged(int value)
    {
        OnPropertyChanged(nameof(EffectiveDataStartRow));
        OnPropertyChanged(nameof(RangeStatusText));
    }

    partial void OnConfiguredStartColumnChanged(int value) => OnPropertyChanged(nameof(RangeStatusText));
    partial void OnConfiguredEndColumnChanged(int value) => OnPropertyChanged(nameof(RangeStatusText));
    partial void OnRangeRequiresReviewChanged(bool value) => OnPropertyChanged(nameof(RangeStatusText));
    partial void OnRangeIsAutomaticCandidateChanged(bool value) => OnPropertyChanged(nameof(RangeStatusText));

    // ---------------------------------------------------------------
    // Sprint 7: direct transfer to the report ("Word'e Aktar")
    // ---------------------------------------------------------------

    private bool CanTransferToReport() =>
        SelectedWorkbook?.SelectedSheetName is not null
        && _currentPreview is { ColumnCount: > 0 }
        && DetectedDataEndRow is { } end
        && end >= EffectiveDataStartRow
        && _workspace.ActiveReport is not null;

    /// <summary>
    /// The panel's primary action: transfer the CURRENT workbook/worksheet/
    /// configured range into the active report — no re-selection dialog, no
    /// mandatory mapping step. Target routing comes from the shared
    /// selection; an already-configured target table opens the small
    /// decision flyout instead of being silently overwritten.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTransferToReport))]
    private void TransferToReport() => ExecuteTransfer(existingTableMode: null, sourceFieldMappings: null);

    [RelayCommand]
    private void TransferRebindKeepColumns()
    {
        IsTransferChoiceOpen = false;
        ExecuteTransfer(ExistingTableTransferMode.RebindKeepColumns, sourceFieldMappings: null);
    }

    [RelayCommand]
    private void TransferReplaceColumnsFromSource()
    {
        IsTransferChoiceOpen = false;
        ExecuteTransfer(ExistingTableTransferMode.ReplaceColumnsFromSource, sourceFieldMappings: null);
    }

    [RelayCommand]
    private void TransferAddAsSource()
    {
        IsTransferChoiceOpen = false;
        ExecuteTransfer(ExistingTableTransferMode.AddAsSource, sourceFieldMappings: null);
    }

    [RelayCommand]
    private void CancelTransferChoice() => IsTransferChoiceOpen = false;

    [RelayCommand]
    private void ApplySourceFieldMapping()
    {
        if (SourceFieldMappingRows.Any(row => string.IsNullOrWhiteSpace(row.SelectedSourceField)))
        {
            StatusText = "Tüm tablo sütunları için bir kaynak alan seçin.";
            return;
        }

        var mappings = SourceFieldMappingRows.Select(row => new TransferSourceFieldMapping
        {
            TableColumnId = row.TableColumnId,
            SourceField = row.SelectedSourceField!
        }).ToList();
        IsSourceFieldMappingOpen = false;
        ExecuteTransfer(ExistingTableTransferMode.AddAsSource, mappings);
    }

    [RelayCommand]
    private void CancelSourceFieldMapping()
    {
        IsSourceFieldMappingOpen = false;
        SourceFieldMappingRows.Clear();
    }

    private void ExecuteTransfer(
        ExistingTableTransferMode? existingTableMode,
        IReadOnlyList<TransferSourceFieldMapping>? sourceFieldMappings)
    {
        var project = _workspace.ActiveProject;
        var report = _workspace.ActiveReport;
        if (project is null || report is null)
        {
            StatusText = "Etkin rapor yok — önce bir proje oluşturun veya açın.";
            return;
        }
        if (SelectedWorkbook?.SelectedSheetName is not { } sheetName || _currentPreview is null)
        {
            StatusText = "Önce bir Excel dosyası ve sayfası açın.";
            return;
        }
        if (DetectedDataEndRow is not { } dataEndRow)
        {
            StatusText = "Önce veri aralığını yapılandırın.";
            return;
        }

        var request = new ExcelTransferRequest
        {
            WorkbookFilePath = SelectedWorkbook.FilePath,
            WorkbookFileName = SelectedWorkbook.DisplayName,
            WorksheetName = sheetName,
            Range = BuildCurrentRange(dataEndRow),
            HeaderTexts = GetHeaderRowTexts(),
            AppliedColumnMappings = _mappingsApplied && ColumnMappings.Count > 0
                ? ColumnMappings.Select(m => new TransferColumnMapping
                {
                    SourceColumn = m.SourceColumn,
                    FieldName = m.FieldName,
                    DataType = m.DataType
                }).ToList()
                : null,
            WorkingDataColumns = GetCurrentWorksheet()?.WorkingData?.Columns.Select(column => new TransferWorkingColumn
            {
                SourceField = column.SourceField,
                Header = column.Header,
                OriginalSourceColumn = column.OriginalSourceColumn
            }).ToList(),
            TargetElementId = _workspace.SelectedReportElementId,
            ExistingTableMode = existingTableMode,
            SourceFieldMappings = sourceFieldMappings,
            PreferredDataSourceName = string.IsNullOrWhiteSpace(DataSourceName) ? null : DataSourceName
        };

        var result = _transferService.Transfer(project, report, request);

        switch (result.Outcome)
        {
            case TransferOutcome.RequiresExistingTableDecision:
                TransferChoiceTableName = result.Table?.Name ?? string.Empty;
                IsTransferChoiceOpen = true;
                break;

            case TransferOutcome.RequiresSourceFieldMapping:
                OpenSourceFieldMapping(result);
                break;

            case TransferOutcome.Success:
                _workspace.SetSelectedReportElement(result.Table!.Id);
                _workspace.NotifyReportContentChanged();
                StatusText = result.AddedAsSource
                    ? $"{result.WorksheetName} · {result.RangeReference} → {result.Table.Name} tablosuna kaynak olarak eklendi"
                    : $"{result.WorksheetName} · {result.RangeReference} → {result.Table.Name} tablosuna aktarıldı";
                _logger.LogInformation(
                    "Transferred {Sheet} {Range} into table {Table} (created: {Created}, additional source: {AddedAsSource})",
                    result.WorksheetName, result.RangeReference, result.Table.Name, result.CreatedNewTable, result.AddedAsSource);
                break;

            case TransferOutcome.Failed:
                StatusText = result.Error!;
                break;
        }
    }

    private void OpenSourceFieldMapping(ExcelTransferResult result)
    {
        SourceFieldMappingRows.Clear();
        foreach (var requirement in result.SourceFieldMappingRequirements)
        {
            var row = new SourceFieldMatchRowViewModel
            {
                TableColumnId = requirement.TableColumnId,
                TableColumnHeader = requirement.TableColumnHeader,
                SelectedSourceField = requirement.SuggestedSourceField
            };
            foreach (var option in requirement.AvailableSourceFields)
            {
                row.AvailableSourceFields.Add(new SourceFieldOptionViewModel
                {
                    SourceField = option.SourceField,
                    DisplayText = option.DisplayText
                });
            }
            SourceFieldMappingRows.Add(row);
        }

        SourceFieldMappingTableName = result.Table?.Name ?? string.Empty;
        IsSourceFieldMappingOpen = true;
        StatusText = "Ek kaynak için çözülemeyen sütunları eşleyin.";
    }

    /// <summary>Raw texts of the configured header row (or empty when the range has no header row) — the default displayed table headers for an unmapped transfer.</summary>
    private IReadOnlyList<string> GetHeaderRowTexts()
    {
        if (GetCurrentWorksheet()?.WorkingData is { } workingData)
            return workingData.Columns.Select(column => column.Header).ToList();

        if (_currentPreview is null || HeaderRowNumber is not { } headerRow) return Array.Empty<string>();

        return ExcelRangeProjection.GetRowTexts(_currentPreview, headerRow, ConfiguredStartColumn, ConfiguredEndColumn);
    }

    // ---------------------------------------------------------------
    // Optional advanced feature: column mapping
    // ---------------------------------------------------------------

    [RelayCommand]
    private void GenerateColumnMappings()
    {
        if (_currentPreview is null) return;

        ColumnMappings.Clear();

        if (GetCurrentWorksheet()?.WorkingData is { } workingData)
        {
            foreach (var column in workingData.Columns)
            {
                ColumnMappings.Add(new ColumnMappingRowViewModel
                {
                    SourceColumn = column.OriginalSourceColumn ?? column.SourceField,
                    FieldName = string.IsNullOrWhiteSpace(column.Header) ? column.SourceField : column.Header,
                    DataType = "string"
                });
            }
            StatusText = $"{ColumnMappings.Count} sütun eşlemesi oluşturuldu — uygulamadan önce gözden geçirin";
            return;
        }

        var headerTexts = HeaderRowNumber is { } headerRow
            ? ExcelRangeProjection.GetRowTexts(_currentPreview, headerRow, ConfiguredStartColumn, ConfiguredEndColumn)
            : Array.Empty<string>();

        for (var sourceColumn = ConfiguredStartColumn; sourceColumn <= ConfiguredEndColumn; sourceColumn++)
        {
            var relativeIndex = sourceColumn - ConfiguredStartColumn;
            var columnLetter = Shared.Spreadsheet.ColumnLetterConverter.ToLetters(sourceColumn);
            var suggestedName = relativeIndex < headerTexts.Count && !string.IsNullOrWhiteSpace(headerTexts[relativeIndex])
                ? headerTexts[relativeIndex]
                : $"Column{columnLetter}";

            ColumnMappings.Add(new ColumnMappingRowViewModel
            {
                SourceColumn = columnLetter,
                FieldName = suggestedName,
                DataType = "string"
            });
        }

        StatusText = $"{ColumnMappings.Count} sütun eşlemesi oluşturuldu — uygulamadan önce gözden geçirin";
    }

    [RelayCommand]
    private async Task SelectSheetTabAsync(string sheetName)
    {
        if (SelectedWorkbook is null || SelectedWorkbook.SelectedSheetName == sheetName) return;

        SelectedWorkbook.SelectedSheetName = sheetName;
        await LoadPreviewAsync();
    }

    [RelayCommand]
    private void OpenMappingDrawer()
    {
        if (ColumnMappings.Count == 0)
            GenerateColumnMappings();
        IsMappingDrawerOpen = true;
    }

    [RelayCommand]
    private void ApplyMapping()
    {
        _mappingsApplied = ColumnMappings.Count > 0;
        IsMappingDrawerOpen = false;
        if (_mappingsApplied)
            StatusText = $"{ColumnMappings.Count} sütun eşlemesi uygulandı — bir sonraki aktarım bu alan adlarını kullanacak";
    }

    [RelayCommand]
    private void CancelMapping() => IsMappingDrawerOpen = false;

    [RelayCommand]
    private void AddDataSourceToProject()
    {
        var project = _workspace.ActiveProject;
        if (project is null)
        {
            StatusText = "Etkin proje yok — önce bir proje oluşturun veya açın.";
            return;
        }
        if (SelectedWorkbook?.SelectedSheetName is not { } selectedSheetName)
        {
            StatusText = "Önce bir çalışma kitabı ve sayfa seçin.";
            return;
        }
        if (string.IsNullOrWhiteSpace(DataSourceName))
        {
            StatusText = "Veri kaynağı için bir ad girin.";
            return;
        }

        if (DetectedDataEndRow is not { } dataEndRow)
        {
            StatusText = "Önce veri aralığını yapılandırın.";
            return;
        }

        var worksheet = new Worksheet
        {
            Name = selectedSheetName,
            SelectedRange = BuildCurrentRange(dataEndRow)
        };

        var dataSource = new ExcelDataSource
        {
            Name = DataSourceName,
            Workbook = new Workbook
            {
                FileName = System.IO.Path.GetFileName(SelectedWorkbook.FilePath),
                SourcePath = SelectedWorkbook.FilePath
            },
            ActiveWorksheetName = worksheet.Name
        };
        dataSource.Workbook.Worksheets.Add(worksheet);

        foreach (var mappingRow in ColumnMappings)
        {
            worksheet.ColumnMappings.Add(new ColumnMapping
            {
                SourceColumn = mappingRow.SourceColumn,
                TargetField = new Domain.DataBinding.DataField
                {
                    Name = mappingRow.FieldName,
                    DataType = mappingRow.DataType
                }
            });
        }

        project.DataSources.Add(dataSource);
        _workspace.SetActiveDataSource(dataSource.Name, worksheet.Name);

        StatusText = $"'{dataSource.Name}' veri kaynağı ({worksheet.ColumnMappings.Count} alan) '{project.Name}' projesine eklendi";
    }

    // ---------------------------------------------------------------
    // Sprint 9: project-owned worksheet working data
    // ---------------------------------------------------------------

    public int ResolveWorkingColumnIndex(string? columnIdentity)
    {
        var workingData = GetCurrentWorksheet()?.WorkingData;
        if (workingData is not null)
            return WorkingDataInteractionResolver.ResolveColumnIndex(workingData, columnIdentity);

        if (string.IsNullOrWhiteSpace(columnIdentity)) return -1;
        try
        {
            var sourceColumn = Shared.Spreadsheet.ColumnLetterConverter.ToIndex(columnIdentity);
            return sourceColumn >= ConfiguredStartColumn && sourceColumn <= ConfiguredEndColumn
                ? sourceColumn - ConfiguredStartColumn
                : -1;
        }
        catch (ArgumentException)
        {
            return -1;
        }
    }

    public async Task CommitCellEditAsync(int gridRowIndex, string columnIdentity, string? value)
    {
        var existingWorkingData = GetCurrentWorksheet()?.WorkingData;
        var originalRowNumber = existingWorkingData is null ? GetPreviewRowNumber(gridRowIndex) : null;
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } workingData) return;

        var workingRowIndex = existingWorkingData is null
            ? workingData.Rows.FindIndex(row => row.OriginalRowNumber == originalRowNumber)
            : MapDisplayRowToWorkingRow(worksheet, workingData, gridRowIndex);
        var columnIndex = WorkingDataInteractionResolver.ResolveColumnIndex(workingData, columnIdentity);
        if (workingRowIndex < 0 || columnIndex < 0)
        {
            StatusText = "Yalnızca yapılandırılmış veri hücreleri düzenlenebilir.";
            RefreshWorkingDataView(worksheet);
            return;
        }

        CompleteMutation(worksheet, _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () => _workingDataService.SetCell(worksheet, workingRowIndex, columnIndex, value)), "Hücre güncellendi · Değiştirildi");
    }

    public async Task ClearCellsAsync(IReadOnlyCollection<GridCellTarget> gridCells)
    {
        if (gridCells.Count == 0)
        {
            StatusText = "Temizlenecek hücre seçilmedi.";
            return;
        }

        var existingWorkingData = GetCurrentWorksheet()?.WorkingData;
        var rowNumberByGridIndex = existingWorkingData is null
            ? gridCells.Select(cell => cell.DisplayRowIndex).Distinct().ToDictionary(index => index, GetPreviewRowNumber)
            : null;
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } workingData) return;

        var cells = gridCells.Select(cell => new WorkingDataCell(
            existingWorkingData is null
                ? workingData.Rows.FindIndex(row => row.OriginalRowNumber == rowNumberByGridIndex![cell.DisplayRowIndex])
                : MapDisplayRowToWorkingRow(worksheet, workingData, cell.DisplayRowIndex),
            WorkingDataInteractionResolver.ResolveColumnIndex(workingData, cell.ColumnIdentity))).ToList();
        if (cells.Any(cell => cell.RowIndex < 0 || cell.ColumnIndex < 0))
        {
            StatusText = "Yalnızca yapılandırılmış veri hücreleri temizlenebilir.";
            RefreshWorkingDataView(worksheet);
            return;
        }

        CompleteMutation(worksheet, _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () => _workingDataService.ClearCells(worksheet, cells)), "Seçili hücreler temizlendi · Değiştirildi");
    }

    public async Task InsertRowRelativeAsync(int displayRowIndex, bool below)
    {
        var existingWorkingData = GetCurrentWorksheet()?.WorkingData;
        var originalRowNumber = existingWorkingData is null ? GetPreviewRowNumber(displayRowIndex) : null;
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } workingData) return;

        var targetIndex = existingWorkingData is null
            ? workingData.Rows.FindIndex(row => row.OriginalRowNumber == originalRowNumber)
            : MapDisplayRowToWorkingRow(worksheet, workingData, displayRowIndex);
        if (targetIndex < 0)
        {
            StatusText = "Yeni satır eklemek için bir veri satırı seçin.";
            RefreshWorkingDataView(worksheet);
            return;
        }

        var insertAt = targetIndex + (below ? 1 : 0);
        CompleteMutation(worksheet, _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () => _workingDataService.InsertRow(worksheet, insertAt)), "Satır eklendi · Değiştirildi");
    }

    public Task InsertRowAsync(int gridRowIndex) => InsertRowRelativeAsync(gridRowIndex, below: true);

    public async Task DeleteRowsAsync(IReadOnlyCollection<int> gridRowIndexes)
    {
        var existingWorkingData = GetCurrentWorksheet()?.WorkingData;
        var originalRows = existingWorkingData is null
            ? gridRowIndexes.Distinct().ToDictionary(index => index, GetPreviewRowNumber)
            : null;
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } workingData) return;

        var rowIndexes = existingWorkingData is null
            ? gridRowIndexes.Select(index => workingData.Rows.FindIndex(row => row.OriginalRowNumber == originalRows![index])).ToList()
            : WorkingDataInteractionResolver.ResolveRowIndexes(workingData, ViewStateFor(worksheet), gridRowIndexes).ToList();
        if (rowIndexes.Count == 0 || rowIndexes.Any(index => index < 0))
        {
            StatusText = "Yalnızca yapılandırılmış veri satırları silinebilir.";
            RefreshWorkingDataView(worksheet);
            return;
        }

        CompleteMutation(worksheet, _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () => _workingDataService.DeleteRows(worksheet, rowIndexes)), "Satır silindi · Değiştirildi");
    }

    public async Task ClearRowsAsync(IReadOnlyCollection<int> gridRowIndexes)
    {
        var existingWorkingData = GetCurrentWorksheet()?.WorkingData;
        var originalRows = existingWorkingData is null
            ? gridRowIndexes.Distinct().ToDictionary(index => index, GetPreviewRowNumber)
            : null;
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } workingData) return;

        var rowIndexes = existingWorkingData is null
            ? gridRowIndexes.Select(index => workingData.Rows.FindIndex(row => row.OriginalRowNumber == originalRows![index])).Where(index => index >= 0).ToList()
            : WorkingDataInteractionResolver.ResolveRowIndexes(workingData, ViewStateFor(worksheet), gridRowIndexes).ToList();
        if (rowIndexes.Count == 0)
        {
            StatusText = "Temizlenecek veri satırı seçilmedi.";
            RefreshWorkingDataView(worksheet);
            return;
        }

        var cells = rowIndexes.SelectMany(rowIndex => Enumerable.Range(0, workingData.Columns.Count).Select(columnIndex => new WorkingDataCell(rowIndex, columnIndex))).ToList();
        CompleteMutation(worksheet, _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () => _workingDataService.ClearCells(worksheet, cells)), "Satır temizlendi · Değiştirildi");
    }

    public async Task InsertColumnRelativeAsync(string columnIdentity, bool right)
    {
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } workingData) return;
        var targetIndex = WorkingDataInteractionResolver.ResolveColumnIndex(workingData, columnIdentity);
        if (targetIndex < 0)
        {
            StatusText = "Yeni sütun eklemek için bir sütun başlığı seçin.";
            return;
        }
        var insertAt = targetIndex + (right ? 1 : 0);
        CompleteMutation(worksheet, _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () => _workingDataService.InsertColumn(worksheet, insertAt)), "Sütun eklendi · Değiştirildi");
    }

    public async Task DeleteColumnsByIdentityAsync(IReadOnlyCollection<string> columnIdentities)
    {
        var project = _workspace.ActiveProject;
        var worksheet = await EnsureWorkingDataAsync();
        if (project is null || worksheet?.WorkingData is not { } workingData || SelectedWorkbook is null) return;
        var dataSource = _workingDataService.FindDataSource(project, SelectedWorkbook.FilePath);
        if (dataSource is null)
        {
            StatusText = "Çalışma verisinin proje veri kaynağı bulunamadı.";
            return;
        }

        var columnIndexes = WorkingDataInteractionResolver.ResolveColumnIndexes(workingData, columnIdentities);
        CompleteMutation(worksheet, _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () => _workingDataService.DeleteColumns(project, dataSource, worksheet, columnIndexes)), "Sütun silindi · Değiştirildi");
    }

    public async Task HideColumnByIdentityAsync(string columnIdentity)
    {
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } data) return;
        var index = WorkingDataInteractionResolver.ResolveColumnIndex(data, columnIdentity);
        if (index < 0) return;
        ViewStateFor(worksheet).SetColumnHidden(data.Columns[index], true);
        RefreshWorkingDataView(worksheet);
    }

    public void RestoreAllHiddenColumns()
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is null) return;
        ViewStateFor(worksheet).RestoreAllColumns();
        RefreshWorkingDataView(worksheet);
    }

    public async Task PasteClipboardAsync(int gridRowIndex, string columnIdentity, string clipboardText)
    {
        var existingWorkingData = GetCurrentWorksheet()?.WorkingData;
        var originalRowNumber = existingWorkingData is null ? GetPreviewRowNumber(gridRowIndex) : null;
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } workingData) return;

        var workingRowIndex = existingWorkingData is null
            ? workingData.Rows.FindIndex(row => row.OriginalRowNumber == originalRowNumber)
            : MapDisplayRowToWorkingRow(worksheet, workingData, gridRowIndex);
        var columnIndex = WorkingDataInteractionResolver.ResolveColumnIndex(workingData, columnIdentity);
        if (workingRowIndex < 0 || columnIndex < 0)
        {
            StatusText = "Yapıştırma başlangıcı yapılandırılmış veri alanında olmalıdır.";
            RefreshWorkingDataView(worksheet);
            return;
        }

        CompleteMutation(
            worksheet,
            _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () => _workingDataService.ApplyClipboardMatrix(worksheet, workingRowIndex, columnIndex, clipboardText)),
            "Pano verisi yapıştırıldı · Değiştirildi");
    }

    [RelayCommand]
    private async Task ResetToSourceAsync()
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is null) return;
        if (IsSourceMissing)
        {
            StatusText = "Kaynak Excel bulunamadı; çalışma verisi korunuyor.";
            return;
        }
        if (!_dialogService.ShowConfirmation(
                "Bu sayfadaki değiştirilmiş çalışma verisi silinecek ve özgün Excel kaynağı yeniden kullanılacak. Devam edilsin mi?",
                "Kaynak Veriye Dön"))
            return;

        _workingDataService.Reset(worksheet);
        _historyRegistry.Forget(worksheet);
        if (_viewStates.TryGetValue(worksheet, out var viewState)) viewState.Clear();
        _findMatches = Array.Empty<WorkingDataCell>();
        _findCursor = -1;
        RowFilterText = string.Empty;
        FindStatusText = string.Empty;
        await LoadPreviewAsync();
        _workspace.NotifyReportContentChanged();
        StatusText = "Kaynak veriye dönüldü";
        PublishWorkingDataState();
        PublishUndoRedoState();
        OnPropertyChanged(nameof(CurrentFindCell));
        OnPropertyChanged(nameof(RowFilterStatusText));
    }

    private WorkingDataHistory HistoryFor(Worksheet worksheet) => _historyRegistry.For(worksheet);

    /// <summary>
    /// Translates a (possibly filtered) DataGrid display row index back to the
    /// true underlying working-data row index. When a preparation row filter is
    /// active the display index is a projection, never product row identity, so
    /// it must be resolved through the stable visible-row mapping.
    /// </summary>
    private int MapDisplayRowToWorkingRow(Worksheet worksheet, WorksheetWorkingData workingData, int displayRowIndex)
    {
        var view = ViewStateFor(worksheet);
        return view.HasRowFilter
            ? view.VisibleRowToWorkingRow(workingData, displayRowIndex)
            : (displayRowIndex >= 0 && displayRowIndex < workingData.Rows.Count ? displayRowIndex : -1);
    }

    private WorkingDataViewState ViewStateFor(Worksheet worksheet) =>
        _viewStates.GetValue(worksheet, _ => new WorkingDataViewState());

    public bool CanUndo => GetCurrentWorksheet() is { WorkingData: not null } worksheet && HistoryFor(worksheet).CanUndo;
    public bool CanRedo => GetCurrentWorksheet() is { WorkingData: not null } worksheet && HistoryFor(worksheet).CanRedo;

    // -----------------------------------------------------------------
    // Sprint 11: Undo / Redo
    // -----------------------------------------------------------------

    [RelayCommand]
    private void Undo()
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is null) return;
        var result = _workingDataService.Undo(worksheet, HistoryFor(worksheet));
        if (result.IsFailure) { StatusText = result.Error!; return; }
        RefreshWorkingDataView(worksheet);
        _workspace.NotifyReportContentChanged();
        StatusText = "Geri alındı";
        PublishUndoRedoState();
    }

    [RelayCommand]
    private void Redo()
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is null) return;
        var result = _workingDataService.Redo(worksheet, HistoryFor(worksheet));
        if (result.IsFailure) { StatusText = result.Error!; return; }
        RefreshWorkingDataView(worksheet);
        _workspace.NotifyReportContentChanged();
        StatusText = "Yinelendi";
        PublishUndoRedoState();
    }

    private void PublishUndoRedoState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // -----------------------------------------------------------------
    // Sprint 11: Find / Replace (current worksheet working data only)
    // -----------------------------------------------------------------

    [ObservableProperty]
    private string _findText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private string _findStatusText = string.Empty;

    /// <summary>Working-data cell selected by the current Find cursor, for the view to scroll/select. Null when no active match.</summary>
    public WorkingDataCell? CurrentFindCell =>
        _findCursor >= 0 && _findCursor < _findMatches.Count ? _findMatches[_findCursor] : null;

    [RelayCommand]
    private async Task FindNextAsync() => await RunFindAsync(forward: true);

    [RelayCommand]
    private async Task FindPreviousAsync() => await RunFindAsync(forward: false);

    private async Task RunFindAsync(bool forward)
    {
        // Find may inspect the current configured preview without creating
        // WorkingData; only Replace lazily creates it. When working data exists
        // we search it, otherwise we search the read-only preview snapshot.
        var worksheet = GetCurrentWorksheet();
        if (string.IsNullOrEmpty(FindText))
        {
            _findMatches = Array.Empty<WorkingDataCell>();
            _findCursor = -1;
            FindStatusText = "Aranacak metin girin.";
            OnPropertyChanged(nameof(CurrentFindCell));
            return;
        }

        _findMatches = worksheet?.WorkingData is not null
            ? _workingDataService.Find(worksheet, FindText)
            : FindInPreview(FindText);

        if (_findMatches.Count == 0)
        {
            _findCursor = -1;
            FindStatusText = "Eşleşme yok";
            OnPropertyChanged(nameof(CurrentFindCell));
            return;
        }

        _findCursor = _findCursor < 0
            ? 0
            : (forward
                ? (_findCursor + 1) % _findMatches.Count
                : (_findCursor - 1 + _findMatches.Count) % _findMatches.Count);
        FindStatusText = $"{_findCursor + 1} / {_findMatches.Count}";
        OnPropertyChanged(nameof(CurrentFindCell));
        await Task.CompletedTask;
    }

    private IReadOnlyList<WorkingDataCell> FindInPreview(string query)
    {
        var matches = new List<WorkingDataCell>();
        if (_currentPreview is null) return matches;
        for (var rowIndex = 0; rowIndex < _currentPreview.Rows.Count; rowIndex++)
        {
            var cells = _currentPreview.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
                if (cells[columnIndex].Contains(query, StringComparison.OrdinalIgnoreCase))
                    matches.Add(new WorkingDataCell(rowIndex, columnIndex));
        }
        return matches;
    }

    [RelayCommand]
    private async Task ReplaceAllAsync()
    {
        if (string.IsNullOrEmpty(FindText))
        {
            FindStatusText = "Aranacak metin girin.";
            return;
        }

        // Replace mutates project-owned data, so it must lazily create working
        // data through the existing creation path before mutating.
        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is null) return;

        var result = _workingDataService.ReplaceAll(worksheet, HistoryFor(worksheet), FindText, ReplaceText ?? string.Empty, out var replaced);
        if (result.IsFailure) { FindStatusText = result.Error!; return; }

        RefreshWorkingDataView(worksheet);
        _workspace.NotifyReportContentChanged();
        FindStatusText = $"{replaced} hücre değiştirildi";
        StatusText = $"Değiştir uygulandı · Değiştirildi";
        PublishUndoRedoState();
        // Re-run find so the match list reflects post-replace content.
        _findMatches = _workingDataService.Find(worksheet, FindText);
        _findCursor = _findMatches.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(CurrentFindCell));
    }

    // -----------------------------------------------------------------
    // Sprint 11: Preparation-only row filter
    // -----------------------------------------------------------------

    [ObservableProperty]
    private string _rowFilterText = string.Empty;

    [ObservableProperty]
    private int _rowFilterColumnIndex;

    public string RowFilterStatusText
    {
        get
        {
            var worksheet = GetCurrentWorksheet();
            if (worksheet?.WorkingData is not { } data) return string.Empty;
            var view = ViewStateFor(worksheet);
            var visible = view.GetVisibleRowIndexes(data).Count;
            return $"{visible} / {data.Rows.Count} satır";
        }
    }

    [RelayCommand]
    private void ApplyRowFilter()
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is null) return;
        ViewStateFor(worksheet).SetRowFilter(RowFilterColumnIndex, RowFilterText);
        RefreshWorkingDataView(worksheet);
        // View state only — deliberately NOT NotifyReportContentChanged.
        OnPropertyChanged(nameof(RowFilterStatusText));
    }

    [RelayCommand]
    private void ClearRowFilter()
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is null) return;
        ViewStateFor(worksheet).ClearRowFilter();
        RowFilterText = string.Empty;
        RefreshWorkingDataView(worksheet);
        OnPropertyChanged(nameof(RowFilterStatusText));
    }

    // -----------------------------------------------------------------
    // Sprint 11: Preparation-only column visibility
    // -----------------------------------------------------------------

    [RelayCommand]
    private void ToggleColumnVisibility(int workingColumnIndex)
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is not { } data) return;
        if (workingColumnIndex < 0 || workingColumnIndex >= data.Columns.Count) return;
        var view = ViewStateFor(worksheet);
        var column = data.Columns[workingColumnIndex];
        view.SetColumnHidden(column, !view.IsColumnHidden(column));
        RefreshWorkingDataView(worksheet);
        // View state only — never changes Preview/Word columns.
    }

    [RelayCommand]
    private void RestoreAllColumns()
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is null) return;
        ViewStateFor(worksheet).RestoreAllColumns();
        RefreshWorkingDataView(worksheet);
    }

    public string? GetWorkingDataColumnHeader(string sourceField) =>
        GetCurrentWorksheet()?.WorkingData?.Columns.FirstOrDefault(column =>
            string.Equals(column.SourceField, sourceField, StringComparison.OrdinalIgnoreCase))?.Header;

    private async Task<Worksheet?> EnsureWorkingDataAsync()
    {
        var project = _workspace.ActiveProject;
        if (project is null)
        {
            StatusText = "Etkin proje yok — çalışma verisi oluşturulamadı.";
            return null;
        }
        if (SelectedWorkbook?.SelectedSheetName is not { } worksheetName || _currentPreview is null)
        {
            StatusText = "Önce bir Excel dosyası ve sayfası açın.";
            return null;
        }
        if (DetectedDataEndRow is not { } dataEndRow || dataEndRow < EffectiveDataStartRow)
        {
            StatusText = "Düzenlemeden önce veri aralığını yapılandırın.";
            return null;
        }

        var range = BuildCurrentRange(dataEndRow);
        var result = await _workingDataService.EnsureCreatedAsync(
            project,
            SelectedWorkbook.FilePath,
            SelectedWorkbook.DisplayName,
            worksheetName,
            range,
            string.IsNullOrWhiteSpace(DataSourceName) ? null : DataSourceName);
        if (result.IsFailure)
        {
            StatusText = result.Error!;
            return null;
        }

        PersistAppliedMappings(result.Value);
        return result.Value;
    }

    private void CompleteMutation(Worksheet worksheet, KKL.WordStudio.Shared.Results.Result result, string successStatus)
    {
        if (result.IsFailure)
        {
            StatusText = result.Error!;
            RefreshWorkingDataView(worksheet);
            return;
        }

        RefreshWorkingDataView(worksheet);
        _workspace.NotifyReportContentChanged();
        StatusText = successStatus;
        PublishWorkingDataState();
        PublishUndoRedoState();
    }

    private void RefreshWorkingDataView(Worksheet worksheet)
    {
        if (worksheet.WorkingData is not { } workingData || SelectedWorkbook?.SelectedSheetName is not { } sheetName) return;
        var viewState = ViewStateFor(worksheet);
        _currentPreview = BuildWorkingDataPreview(sheetName, workingData);
        PreviewTable = BuildWorkingDataTable(workingData, viewState);
        LoadPersistedMappings(worksheet);
        SetWorkspaceDataSource(sheetName);
        TransferToReportCommand.NotifyCanExecuteChanged();
        PublishWorkingDataState();
        PublishUndoRedoState();
        OnPropertyChanged(nameof(RowFilterStatusText));
    }

    private Worksheet? GetCurrentWorksheet()
    {
        var project = _workspace.ActiveProject;
        if (project is null || SelectedWorkbook?.SelectedSheetName is not { } sheetName) return null;
        return _workingDataService.FindWorksheet(project, SelectedWorkbook.FilePath, sheetName);
    }

    private int? GetPreviewRowNumber(int gridRowIndex) =>
        _currentPreview is not null && gridRowIndex >= 0 && gridRowIndex < _currentPreview.RowNumbers.Count
            ? _currentPreview.RowNumbers[gridRowIndex]
            : null;

    private void ApplyWorksheetRange(Worksheet worksheet)
    {
        var range = worksheet.SelectedRange;
        if (range is null) return;
        StartRowIsHeader = range.HeaderRowIndex.HasValue;
        StartRow = range.HeaderRowIndex ?? range.DataStartRow;
        ConfiguredDataStartRow = range.DataStartRow;
        ConfiguredStartColumn = range.StartColumn ?? 1;
        ConfiguredEndColumn = range.EndColumn ?? Math.Max(ConfiguredStartColumn, _currentPreview?.ColumnCount ?? ConfiguredStartColumn);
        DetectedDataEndRow = range.DataEndRow;
        WasAutoDetected = range.WasAutoDetected;
        RangeIsAutomaticCandidate = false;
        RangeRequiresReview = false;
    }

    private void ResetCurrentRangeCandidate(int previewColumnCount)
    {
        StartRowIsHeader = false;
        StartRow = 1;
        ConfiguredDataStartRow = 1;
        ConfiguredStartColumn = 1;
        ConfiguredEndColumn = Math.Max(1, previewColumnCount);
        DetectedDataEndRow = null;
        WasAutoDetected = false;
        RangeIsAutomaticCandidate = false;
        RangeRequiresReview = false;
    }

    private DataRange BuildCurrentRange(int dataEndRow) => new()
    {
        DataStartRow = EffectiveDataStartRow,
        DataEndRow = dataEndRow,
        HeaderRowIndex = HeaderRowNumber,
        StartColumn = Math.Max(1, ConfiguredStartColumn),
        EndColumn = Math.Max(Math.Max(1, ConfiguredStartColumn), ConfiguredEndColumn),
        WasAutoDetected = WasAutoDetected
    };

    private static DataRange CloneRange(DataRange range) => new()
    {
        DataStartRow = range.DataStartRow,
        DataEndRow = range.DataEndRow,
        HeaderRowIndex = range.HeaderRowIndex,
        StartColumn = range.StartColumn,
        EndColumn = range.EndColumn,
        WasAutoDetected = range.WasAutoDetected
    };

    [RelayCommand]
    private void OpenRangeEditor()
    {
        EditorHasHeader = HeaderRowNumber.HasValue;
        EditorHeaderRow = HeaderRowNumber;
        EditorDataStartRow = EffectiveDataStartRow;
        EditorDataEndRow = DetectedDataEndRow ?? EffectiveDataStartRow;
        EditorStartColumn = Shared.Spreadsheet.ColumnLetterConverter.ToLetters(Math.Max(1, ConfiguredStartColumn));
        EditorEndColumn = Shared.Spreadsheet.ColumnLetterConverter.ToLetters(Math.Max(Math.Max(1, ConfiguredStartColumn), ConfiguredEndColumn));
        IsRangeEditorOpen = true;
    }

    [RelayCommand]
    private void CancelRangeEditor() => IsRangeEditorOpen = false;

    [RelayCommand]
    private async Task RedetectRangeAsync()
    {
        await AutoDetectCurrentRangeAsync();
        if (DetectedDataEndRow is not null) OpenRangeEditor();
    }

    [RelayCommand]
    private void ApplyRangeEditor()
    {
        int startColumn;
        int endColumn;
        try
        {
            startColumn = Shared.Spreadsheet.ColumnLetterConverter.ToIndex(EditorStartColumn.Trim());
            endColumn = Shared.Spreadsheet.ColumnLetterConverter.ToIndex(EditorEndColumn.Trim());
        }
        catch (ArgumentException)
        {
            StatusText = "Başlangıç ve bitiş sütunlarını A, B, F gibi Excel harfleriyle girin.";
            return;
        }

        if (EditorDataStartRow < 1 || EditorDataEndRow < EditorDataStartRow || startColumn < 1 || endColumn < startColumn)
        {
            StatusText = "Veri aralığı sınırlarını kontrol edin.";
            return;
        }
        if (EditorHasHeader && (EditorHeaderRow is null || EditorHeaderRow < 1 || EditorHeaderRow >= EditorDataStartRow))
        {
            StatusText = "Başlık satırı veri başlangıcından önce olmalıdır.";
            return;
        }

        StartRowIsHeader = EditorHasHeader;
        StartRow = EditorHasHeader ? EditorHeaderRow!.Value : EditorDataStartRow;
        ConfiguredDataStartRow = EditorDataStartRow;
        ConfiguredStartColumn = startColumn;
        ConfiguredEndColumn = endColumn;
        DetectedDataEndRow = EditorDataEndRow;
        WasAutoDetected = false;
        RangeIsAutomaticCandidate = false;
        RangeRequiresReview = false;
        IsRangeEditorOpen = false;

        var worksheet = GetCurrentWorksheet();
        if (worksheet is not null)
            worksheet.SelectedRange = CloneRange(BuildCurrentRange(EditorDataEndRow));

        StatusText = "Veri aralığı yapılandırıldı";
        OnPropertyChanged(nameof(RangeStatusText));
        TransferToReportCommand.NotifyCanExecuteChanged();
    }

    public void ShowHeaderSelectionHint() => StatusText = "Önce bir satır veya sütun başlığı seçin.";

    private void LoadPersistedMappings(Worksheet? worksheet)
    {
        ColumnMappings.Clear();
        if (worksheet is null)
        {
            _mappingsApplied = false;
            return;
        }

        foreach (var mapping in worksheet.ColumnMappings)
        {
            ColumnMappings.Add(new ColumnMappingRowViewModel
            {
                SourceColumn = mapping.SourceColumn,
                FieldName = mapping.TargetField.Name,
                DataType = mapping.TargetField.DataType
            });
        }
        _mappingsApplied = ColumnMappings.Count > 0;
    }

    private void PersistAppliedMappings(Worksheet worksheet)
    {
        if (!_mappingsApplied || ColumnMappings.Count == 0 || worksheet.ColumnMappings.Count > 0) return;
        foreach (var mappingRow in ColumnMappings)
        {
            worksheet.ColumnMappings.Add(new ColumnMapping
            {
                SourceColumn = mappingRow.SourceColumn,
                TargetField = new Domain.DataBinding.DataField
                {
                    Name = mappingRow.FieldName,
                    DataType = mappingRow.DataType
                }
            });
        }
    }

    private void SetWorkspaceDataSource(string sheetName)
    {
        var dataSourceName = _workspace.ActiveProject is { } project && SelectedWorkbook is not null
            ? _workingDataService.FindDataSource(project, SelectedWorkbook.FilePath)?.Name
            : null;
        _workspace.SetActiveDataSource(dataSourceName ?? DataSourceName, sheetName);
        _workspace.SetPreviewActive(true);
    }

    private void PublishWorkingDataState()
    {
        OnPropertyChanged(nameof(HasWorkingData));
        OnPropertyChanged(nameof(IsSourceMissing));
        OnPropertyChanged(nameof(CanResetWorkingData));
        OnPropertyChanged(nameof(WorkingDataStateText));
    }

    private static SheetPreview BuildWorkingDataPreview(string worksheetName, WorksheetWorkingData workingData) => new()
    {
        WorksheetName = worksheetName,
        RowNumbers = workingData.Rows.Select((row, index) => row.OriginalRowNumber ?? index + 1).ToList(),
        Rows = workingData.Rows.Select(row => (IReadOnlyList<string>)row.Values.Select(value => value ?? string.Empty).ToList()).ToList(),
        ColumnCount = workingData.Columns.Count,
        IsTruncated = false
    };

    private static DataTable BuildWorkingDataTable(WorksheetWorkingData workingData, WorkingDataViewState viewState)
    {
        // Preparation-only projection: hidden columns are omitted from the grid
        // and filtered-out rows are not shown. This never mutates workingData;
        // the row-number column ("#") always stays visible.
        var visibleColumns = viewState.GetVisibleColumnIndexes(workingData);
        var visibleRows = viewState.GetVisibleRowIndexes(workingData);

        var table = new DataTable();
        table.Columns.Add("#");
        foreach (var columnIndex in visibleColumns)
            table.Columns.Add(workingData.Columns[columnIndex].SourceField);

        foreach (var rowIndex in visibleRows)
        {
            var workingRow = workingData.Rows[rowIndex];
            var dataRow = table.NewRow();
            dataRow[0] = workingRow.OriginalRowNumber?.ToString() ?? $"P{rowIndex + 1}";
            var target = 1;
            foreach (var columnIndex in visibleColumns)
            {
                dataRow[target++] = columnIndex < workingRow.Values.Count ? workingRow.Values[columnIndex] ?? string.Empty : string.Empty;
            }
            table.Rows.Add(dataRow);
        }
        return table;
    }

    private static DataTable BuildPreviewTable(SheetPreview preview)
    {
        var table = new DataTable();
        table.Columns.Add("#"); // row-number column, satisfies "row headers shown"

        for (var i = 0; i < preview.ColumnCount; i++)
            table.Columns.Add(Shared.Spreadsheet.ColumnLetterConverter.ToLetters(i + 1));

        for (var rowIndex = 0; rowIndex < preview.Rows.Count; rowIndex++)
        {
            var dataRow = table.NewRow();
            dataRow[0] = preview.RowNumbers[rowIndex];

            var cells = preview.Rows[rowIndex];
            for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
                dataRow[columnIndex + 1] = cells[columnIndex];

            table.Rows.Add(dataRow);
        }

        return table;
    }

    partial void OnSelectedWorkbookChanged(OpenWorkbookViewModel? value)
    {
        OnPropertyChanged(nameof(HasOpenWorkbook));
        PublishWorkingDataState();
        if (value is not null)
            _ = LoadPreviewAsync();
    }
}
public readonly record struct GridCellTarget(int DisplayRowIndex, string ColumnIdentity);

