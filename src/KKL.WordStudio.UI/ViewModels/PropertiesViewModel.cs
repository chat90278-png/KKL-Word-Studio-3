namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.DataSources;
using KKL.WordStudio.Application.Editing;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Visitors;
using KKL.WordStudio.UI.Services;

/// <summary>Which kind of element the Properties page is currently showing fields for — drives which section of PropertiesView.xaml is visible.</summary>
public enum PropertiesSelectionKind { None, Heading, Table }

/// <summary>
/// Contextual Properties for whatever Contents node is selected
/// (Workspace.SelectedReportElementId): Heading fields for a Heading/Alt
/// Heading TextElement, Table fields (+ Binding) for a TableElement.
/// Replaces the old permanent Table-only properties panel — Heading
/// selection previously showed nothing at all.
///
/// Only currently-real Domain/Application capabilities are exposed —
/// no invented UI-only persisted properties (Start New Page/Keep With
/// Next/Numbering from the HTML reference are NOT shown here because
/// nothing in the current Domain model supports them yet).
/// </summary>
public sealed partial class PropertiesViewModel : ViewModelBase
{
    private readonly IWorkspace _workspace;
    private readonly DockViewModel _dock;
    private readonly IReportEditingService _editingService;
    private readonly ITableSourceCompositionService _tableSourceService;
    private readonly IReferenceDocumentFormatProvider _referenceFormatProvider;
    private readonly ITableFormatSelectionService _tableFormatSelectionService;
    private readonly ISerialQuantityGroupingConfigurationService _groupingConfigurationService;
    private readonly IDialogService _dialogService;

    private TableElement? _selectedTable;
    private TextElement? _selectedHeading;
    private DocumentFormatProfile? _resolvedFormatProfile;
    private bool _isRefreshingTableFormatSelection;
    private int _tableFormatRefreshGeneration;

    public ObservableCollection<BindingCandidateViewModel> BindingCandidates { get; } = new();
    public ObservableCollection<HeadingCaptionCandidateViewModel> HeadingCaptionCandidates { get; } = new();
    public ObservableCollection<TableSourceRowViewModel> TableSources { get; } = new();
    public ObservableCollection<TableFormatOptionViewModel> TableFormatOptions { get; } = new();
    public ObservableCollection<GroupingColumnChoiceViewModel> GroupingColumns { get; } = new();

    [ObservableProperty]
    private PropertiesSelectionKind _selectionKind = PropertiesSelectionKind.None;

    // ---- Heading fields ----
    [ObservableProperty]
    private string _headingText = string.Empty;

    [ObservableProperty]
    private bool _isMainHeading = true;

    // ---- Table fields ----
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _tableCaption = string.Empty;

    [ObservableProperty]
    private HeadingCaptionCandidateViewModel? _selectedHeadingCaptionCandidate;

    [ObservableProperty]
    private bool _isBold;

    [ObservableProperty]
    private double _fontSize;

    [ObservableProperty]
    private bool _showHeader;

    [ObservableProperty]
    private bool _hasBinding;

    [ObservableProperty]
    private string _boundDataSourceDisplay = string.Empty;

    [ObservableProperty]
    private string _boundWorksheetRangeDisplay = string.Empty;

    // ---- Sprint 16 reference table format ----
    [ObservableProperty]
    private TableFormatOptionViewModel? _selectedTableFormatOption;

    [ObservableProperty]
    private string _tableFormatStatusText = string.Empty;

    // ---- Sprint 16 serial/quantity grouping diagnostics ----
    [ObservableProperty]
    private string _groupingStatusText = string.Empty;

    [ObservableProperty]
    private bool _isGroupingEditorOpen;

    [ObservableProperty]
    private bool _canRemoveGrouping;

    [ObservableProperty]
    private GroupingColumnChoiceViewModel? _selectedMatchKeyColumn;

    [ObservableProperty]
    private GroupingColumnChoiceViewModel? _selectedSerialColumn;

    [ObservableProperty]
    private GroupingColumnChoiceViewModel? _selectedQuantityColumn;

    public PropertiesViewModel(
        IWorkspace workspace,
        DockViewModel dock,
        IReportEditingService editingService,
        ITableSourceCompositionService tableSourceService,
        IReferenceDocumentFormatProvider referenceFormatProvider,
        ISerialQuantityGroupingDetector groupingDetector,
        IDialogService dialogService)
    {
        _workspace = workspace;
        _dock = dock;
        _editingService = editingService;
        _tableSourceService = tableSourceService;
        _referenceFormatProvider = referenceFormatProvider;
        _tableFormatSelectionService = new TableFormatSelectionService();
        _groupingConfigurationService = new SerialQuantityGroupingConfigurationService(groupingDetector);
        _dialogService = dialogService;
        _workspace.WorkspaceChanged += (_, _) => Refresh();
        _workspace.ReportContentChanged += (_, _) => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        var report = _workspace.ActiveReport;
        var elementId = _workspace.SelectedReportElementId;
        var element = report is not null && elementId is not null
            ? ReportElementFlattener.FindById(report, elementId.Value)
            : null;

        _selectedTable = element as TableElement;
        _selectedHeading = element as TextElement;

        SelectionKind = element switch
        {
            TableElement => PropertiesSelectionKind.Table,
            TextElement => PropertiesSelectionKind.Heading,
            _ => PropertiesSelectionKind.None
        };

        if (_selectedHeading is not null)
        {
            HeadingText = _selectedHeading.Content.Text;
            IsMainHeading = HeadingStylePresets.IsHeading(_selectedHeading.Style);
        }

        if (_selectedTable is not null)
        {
            Name = _selectedTable.Name;
            Description = _selectedTable.Description ?? string.Empty;
            TableCaption = _selectedTable.Caption ?? string.Empty;
            RefreshHeadingCaptionCandidates(report!);
            IsBold = _selectedTable.Style.Bold;
            FontSize = _selectedTable.Style.FontSize;
            ShowHeader = _selectedTable.Rows.Any(r => r.Kind == TableRowKind.Header);
            RefreshTableSources();
            RefreshGroupingState();
            _ = RefreshTableFormatOptionsAsync(_selectedTable.Id);
        }
        else
        {
            GroupingStatusText = string.Empty;
            IsGroupingEditorOpen = false;
            CanRemoveGrouping = false;
            GroupingColumns.Clear();
            TableFormatOptions.Clear();
            SelectedTableFormatOption = null;
            TableFormatStatusText = string.Empty;
        }
    }

    private void RefreshTableSources()
    {
        TableSources.Clear();
        BoundDataSourceDisplay = string.Empty;
        BoundWorksheetRangeDisplay = string.Empty;

        var project = _workspace.ActiveProject;
        if (_selectedTable is null || project is null)
        {
            HasBinding = false;
            return;
        }

        foreach (var descriptor in _tableSourceService.DescribeSources(project, _selectedTable))
        {
            TableSources.Add(new TableSourceRowViewModel
            {
                Index = descriptor.Index,
                DataSourceName = descriptor.DataSourceName,
                WorksheetName = descriptor.WorksheetName,
                RangeText = descriptor.RangeText,
                StatusText = descriptor.StatusText,
                IsUsable = descriptor.IsUsable,
                IsLegacyBinding = descriptor.IsLegacyBinding
            });
        }

        HasBinding = TableSources.Count > 0;
        if (TableSources.Count == 1)
        {
            BoundDataSourceDisplay = TableSources[0].DataSourceName;
            BoundWorksheetRangeDisplay = TableSources[0].WorksheetRangeDisplay;
        }
    }

    private void RefreshHeadingCaptionCandidates(KKL.WordStudio.Domain.Reports.Report report)
    {
        HeadingCaptionCandidates.Clear();
        foreach (var heading in ReportElementFlattener.Flatten(report).OfType<TextElement>()
                     .Where(h => HeadingStylePresets.IsHeading(h.Style) || HeadingStylePresets.IsAltHeading(h.Style)))
        {
            HeadingCaptionCandidates.Add(new HeadingCaptionCandidateViewModel
            {
                ElementId = heading.Id,
                Text = heading.Content.Text,
                Level = HeadingStylePresets.IsHeading(heading.Style) ? "Başlık 1" : "Başlık 2"
            });
        }
        SelectedHeadingCaptionCandidate = null;
    }

    [RelayCommand]
    private void UseSelectedHeadingAsTableCaption()
    {
        var report = _workspace.ActiveReport;
        var candidate = SelectedHeadingCaptionCandidate;
        if (report is null || _selectedTable is null || candidate is null) return;

        var result = _editingService.UseHeadingTextAsTableCaption(report, _selectedTable.Id, candidate.ElementId);
        if (result.IsSuccess)
        {
            TableCaption = _selectedTable.Caption ?? string.Empty;
            _workspace.NotifyReportContentChanged();
        }
    }

    [RelayCommand]
    private void ApplyHeadingChanges()
    {
        if (_selectedHeading is null) return;

        _selectedHeading.Content = Expression.Literal(HeadingText);
        _selectedHeading.Style = IsMainHeading ? HeadingStylePresets.CreateHeadingStyle() : HeadingStylePresets.CreateAltHeadingStyle();
        _workspace.NotifyReportContentChanged();
    }

    [RelayCommand]
    private void ApplyTableChanges()
    {
        var report = _workspace.ActiveReport;
        if (_selectedTable is null || report is null) return;

        _selectedTable.Name = Name;
        _selectedTable.Description = string.IsNullOrWhiteSpace(Description) ? null : Description;
        var captionResult = _editingService.CommitTableCaption(report, _selectedTable.Id, TableCaption);
        if (captionResult.IsFailure) return;
        _selectedTable.Style.Bold = IsBold;
        _selectedTable.Style.FontSize = FontSize;
        ApplyShowHeaderToggle();
        _workspace.NotifyReportContentChanged();
    }

    private void ApplyShowHeaderToggle()
    {
        if (_selectedTable is null) return;

        var hasHeaderRow = _selectedTable.Rows.Any(r => r.Kind == TableRowKind.Header);
        if (ShowHeader && !hasHeaderRow)
        {
            _selectedTable.Rows.Insert(0, new TableRow { Kind = TableRowKind.Header });
        }
        else if (!ShowHeader && hasHeaderRow)
        {
            foreach (var row in _selectedTable.Rows.Where(r => r.Kind == TableRowKind.Header).ToList())
                _selectedTable.Rows.Remove(row);
        }
    }

    partial void OnSelectedTableFormatOptionChanged(TableFormatOptionViewModel? value)
    {
        if (_isRefreshingTableFormatSelection || _selectedTable is null || value is null)
            return;

        var result = _tableFormatSelectionService.Apply(_selectedTable, _resolvedFormatProfile, value.Key);
        if (result.IsFailure)
        {
            TableFormatStatusText = result.Error!;
            _ = RefreshTableFormatOptionsAsync(_selectedTable.Id);
            return;
        }

        TableFormatStatusText = string.IsNullOrWhiteSpace(value.Key)
            ? "Varsayılan tablo biçimi kullanılacak."
            : $"Tablo biçimi: {value.DisplayName}";
        _workspace.NotifyReportContentChanged();
    }

    private async Task RefreshTableFormatOptionsAsync(Guid tableId)
    {
        var generation = Interlocked.Increment(ref _tableFormatRefreshGeneration);
        var project = _workspace.ActiveProject;
        if (project is null)
            return;

        ReferenceDocumentFormatResult result;
        try
        {
            result = await _referenceFormatProvider.ReadAsync(project);
        }
        catch (Exception ex)
        {
            if (generation == _tableFormatRefreshGeneration && _selectedTable?.Id == tableId)
                TableFormatStatusText = $"Biçim şablonu okunamadı: {ex.Message}";
            return;
        }

        if (generation != _tableFormatRefreshGeneration || _selectedTable?.Id != tableId)
            return;

        _resolvedFormatProfile = result.Profile;
        _isRefreshingTableFormatSelection = true;
        try
        {
            TableFormatOptions.Clear();
            var defaultOption = new TableFormatOptionViewModel
            {
                Key = null,
                DisplayName = "Varsayılan"
            };
            TableFormatOptions.Add(defaultOption);

            if (result.Profile is not null)
            {
                foreach (var referenceFormat in result.Profile.TableFormats)
                {
                    TableFormatOptions.Add(new TableFormatOptionViewModel
                    {
                        Key = referenceFormat.Key,
                        DisplayName = referenceFormat.DisplayName
                    });
                }
            }

            SelectedTableFormatOption = TableFormatOptions.FirstOrDefault(option =>
                    string.Equals(option.Key, _selectedTable.ReferenceTableFormatKey, StringComparison.Ordinal))
                ?? defaultOption;

            TableFormatStatusText = result.IsMissing
                ? result.StatusMessage ?? "Biçim şablonu bulunamadı."
                : result.Profile is null
                    ? "Biçim şablonu eklenmedi; varsayılan tablo biçimi kullanılacak."
                    : string.Empty;
        }
        finally
        {
            _isRefreshingTableFormatSelection = false;
        }
    }

    private void RefreshGroupingState()
    {
        GroupingColumns.Clear();
        if (_selectedTable is null)
        {
            GroupingStatusText = string.Empty;
            CanRemoveGrouping = false;
            return;
        }

        foreach (var column in _selectedTable.Columns)
        {
            GroupingColumns.Add(new GroupingColumnChoiceViewModel
            {
                ColumnId = column.Id,
                DisplayName = !string.IsNullOrWhiteSpace(column.Header)
                    ? column.Header
                    : column.SourceField ?? "(adsız sütun)"
            });
        }

        var diagnosis = _groupingConfigurationService.Diagnose(_selectedTable);
        GroupingStatusText = diagnosis.StatusMessage;
        CanRemoveGrouping = _selectedTable.SerialQuantityGrouping is not null;
    }

    [RelayCommand]
    private void AutoDetectGrouping()
    {
        if (_selectedTable is null)
            return;

        var result = _groupingConfigurationService.AutoDetect(_selectedTable);
        if (result.IsFailure)
        {
            GroupingStatusText = result.Error!;
            return;
        }

        IsGroupingEditorOpen = false;
        RefreshGroupingState();
        _workspace.NotifyReportContentChanged();
    }

    [RelayCommand]
    private void EditGrouping()
    {
        if (_selectedTable is null)
            return;

        var configuration = _selectedTable.SerialQuantityGrouping;
        var diagnosis = _groupingConfigurationService.Diagnose(_selectedTable);

        SelectedMatchKeyColumn = FindGroupingChoice(configuration?.MatchKeyColumnId ?? diagnosis.MatchKeyColumn?.Id);
        SelectedSerialColumn = FindGroupingChoice(configuration?.SerialNumberColumnId ?? diagnosis.SerialColumn?.Id);
        SelectedQuantityColumn = FindGroupingChoice(configuration?.QuantityColumnId ?? diagnosis.QuantityColumn?.Id);
        IsGroupingEditorOpen = true;
    }

    [RelayCommand]
    private void ApplyManualGrouping()
    {
        if (_selectedTable is null
            || SelectedMatchKeyColumn is null
            || SelectedSerialColumn is null
            || SelectedQuantityColumn is null)
        {
            GroupingStatusText = "Üç rol alanını da seçin.";
            return;
        }

        var result = _groupingConfigurationService.ApplyManual(
            _selectedTable,
            SelectedMatchKeyColumn.ColumnId,
            SelectedSerialColumn.ColumnId,
            SelectedQuantityColumn.ColumnId);
        if (result.IsFailure)
        {
            GroupingStatusText = result.Error!;
            return;
        }

        IsGroupingEditorOpen = false;
        RefreshGroupingState();
        _workspace.NotifyReportContentChanged();
    }

    [RelayCommand]
    private void CancelGroupingEdit() => IsGroupingEditorOpen = false;

    [RelayCommand]
    private void RemoveGrouping()
    {
        if (_selectedTable?.SerialQuantityGrouping is null)
            return;

        if (!_dialogService.ShowConfirmation("Seri No / Adet düzeni kaldırılsın mı?", "Düzeni Kaldır"))
            return;

        _groupingConfigurationService.Remove(_selectedTable);
        IsGroupingEditorOpen = false;
        RefreshGroupingState();
        _workspace.NotifyReportContentChanged();
    }

    private GroupingColumnChoiceViewModel? FindGroupingChoice(Guid? columnId) =>
        columnId is null
            ? null
            : GroupingColumns.FirstOrDefault(choice => choice.ColumnId == columnId.Value);

    [RelayCommand]
    private void ChangeBinding()
    {
        BindingCandidates.Clear();
        var project = _workspace.ActiveProject;
        if (project is null) return;

        foreach (var dataSource in project.DataSources.OfType<ExcelDataSource>())
        {
            foreach (var worksheet in dataSource.Workbook.Worksheets)
            {
                var isConfigured = worksheet.SelectedRange is not null;
                BindingCandidates.Add(new BindingCandidateViewModel
                {
                    DataSourceName = dataSource.Name,
                    WorksheetName = worksheet.Name,
                    RangeText = isConfigured ? worksheet.SelectedRange!.RangeReference : "Aralık ayarlanmadı",
                    IsConfigured = isConfigured
                });
            }
        }

        _dock.ShowChangeBindingCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectBindingCandidate(BindingCandidateViewModel candidate)
    {
        foreach (var c in BindingCandidates) c.IsSelected = ReferenceEquals(c, candidate);
    }

    [RelayCommand]
    private void BindSelectedRange()
    {
        var candidate = BindingCandidates.FirstOrDefault(c => c.IsSelected);
        if (candidate is null || _selectedTable is null) return;

        // WorksheetName pins this table to the chosen worksheet specifically —
        // this is exactly the fix from ADR 0009: without it, every table bound
        // to the same DataSource would follow whichever worksheet is merely
        // "active" at export/preview time.
        _selectedTable.Sources.Clear();
        _selectedTable.Binding = new Binding { DataSourceName = candidate.DataSourceName, WorksheetName = candidate.WorksheetName };
        RefreshTableSources();
        _workspace.NotifyReportContentChanged();
        _dock.ShowPropertiesCommand.Execute(null);
    }

    [RelayCommand]
    private void ClearBinding()
    {
        if (_selectedTable is null) return;
        _selectedTable.Sources.Clear();
        _selectedTable.Binding = null;
        RefreshTableSources();
        _workspace.NotifyReportContentChanged();
    }

    [RelayCommand]
    private void MoveTableSourceUp(TableSourceRowViewModel source) => MoveTableSource(source, -1);

    [RelayCommand]
    private void MoveTableSourceDown(TableSourceRowViewModel source) => MoveTableSource(source, 1);

    private void MoveTableSource(TableSourceRowViewModel source, int offset)
    {
        if (_selectedTable is null) return;
        var result = _tableSourceService.MoveSource(_selectedTable, source.Index, offset);
        if (result.IsFailure) return;
        RefreshTableSources();
        _workspace.NotifyReportContentChanged();
    }

    [RelayCommand]
    private void RemoveTableSource(TableSourceRowViewModel source)
    {
        if (_selectedTable is null) return;
        var result = _tableSourceService.RemoveSource(_selectedTable, source.Index);
        if (result.IsFailure) return;
        RefreshTableSources();
        _workspace.NotifyReportContentChanged();
    }

    [RelayCommand]
    private void BackFromBinding() => _dock.ShowPropertiesCommand.Execute(null);
}
