namespace KKL.WordStudio.UI.ViewModels;

using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Domain.Visitors;
using KKL.WordStudio.Shared.Results;
using KKL.WordStudio.Shared.Spreadsheet;

public sealed partial class ExcelWorkspaceViewModel
{
    private readonly ExcelSemanticFieldMatcher _semanticFieldMatcher = new();

    [ObservableProperty]
    private bool _isTransferPlacementOpen;

    [ObservableProperty]
    private bool _hasExistingTransferTarget;

    [ObservableProperty]
    private bool _updateExistingTable;

    [ObservableProperty]
    private bool _createNewTable = true;

    [ObservableProperty]
    private bool _includePlacementHeading = true;

    [ObservableProperty]
    private bool _includePlacementAltHeading = true;

    [ObservableProperty]
    private string _placementParentText = $"1. {ExcelTransferPlacementCoordinator.DefaultRootHeadingText}";

    [ObservableProperty]
    private string _placementHeadingNumber = "1.1";

    [ObservableProperty]
    private string _placementAltHeadingNumber = "1.1.1";

    [ObservableProperty]
    private string _placementHeadingText = "Yeni başlık";

    [ObservableProperty]
    private string _placementAltHeadingText = "Yeni alt başlık";

    [ObservableProperty]
    private string _placementTableName = "Tablo 1";

    [ObservableProperty]
    private string _existingTransferTargetText = string.Empty;

    private Guid? _placementExistingTableId;
    private Guid? _placementAnchorElementId;

    partial void OnPreviewTableChanged(DataTable value)
    {
        InjectWorkingDataHeaderRow(value);
        ApplyPersistedHeaderOverrides(value);
        OpenTransferPlacementCommand.NotifyCanExecuteChanged();
    }

    partial void OnDetectedDataEndRowChanged(int? value) =>
        OpenTransferPlacementCommand.NotifyCanExecuteChanged();

    partial void OnUpdateExistingTableChanged(bool value)
    {
        if (value && CreateNewTable)
            CreateNewTable = false;
    }

    partial void OnCreateNewTableChanged(bool value)
    {
        if (value && UpdateExistingTable)
            UpdateExistingTable = false;
    }

    partial void OnIncludePlacementHeadingChanged(bool value) => RefreshPlacementNumbers();
    partial void OnIncludePlacementAltHeadingChanged(bool value) => RefreshPlacementNumbers();

    private bool CanOpenTransferPlacement() =>
        SelectedWorkbook?.SelectedSheetName is not null
        && _currentPreview is { ColumnCount: > 0 }
        && DetectedDataEndRow is { } end
        && end >= EffectiveDataStartRow
        && _workspace.ActiveReport is not null;

    [RelayCommand(CanExecute = nameof(CanOpenTransferPlacement))]
    private void OpenTransferPlacement()
    {
        var report = _workspace.ActiveReport;
        if (report is null)
        {
            StatusText = "Etkin rapor yok — önce bir proje oluşturun veya açın.";
            return;
        }

        EnsureColumnTransferOptions();
        if (ColumnMappings.All(option => !option.IsIncluded))
        {
            StatusText = "Aktarılacak en az bir sütun seçin.";
            return;
        }

        var selected = _workspace.SelectedReportElementId is { } selectedId
            ? ReportElementFlattener.FindById(report, selectedId)
            : null;
        var selectedTable = selected as TableElement;

        _placementAnchorElementId = selected?.Id;
        _placementExistingTableId = selectedTable?.Id;
        HasExistingTransferTarget = selectedTable is not null;
        ExistingTransferTargetText = selectedTable is null
            ? string.Empty
            : $"Var olan {selectedTable.Name} tablosunu güncelle";
        UpdateExistingTable = selectedTable is not null;
        CreateNewTable = selectedTable is null;

        PlacementTableName = selectedTable?.Name ?? NextTableName(report);
        PlacementHeadingText = "Yeni başlık";
        PlacementAltHeadingText = "Yeni alt başlık";
        IncludePlacementHeading = true;
        IncludePlacementAltHeading = true;
        PlacementParentText = ResolvePlacementParentText(report);
        RefreshPlacementNumbers();
        IsTransferPlacementOpen = true;
    }

    [RelayCommand]
    private void RemovePlacementHeading() => IncludePlacementHeading = false;

    [RelayCommand]
    private void RemovePlacementAltHeading() => IncludePlacementAltHeading = false;

    [RelayCommand]
    private void CancelTransferPlacement() => IsTransferPlacementOpen = false;

    [RelayCommand]
    private void ConfirmTransferPlacement()
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

        EnsureColumnTransferOptions();
        var selectedOptions = ColumnMappings.Where(option => option.IsIncluded).ToList();
        if (selectedOptions.Count == 0)
        {
            StatusText = "Aktarılacak en az bir sütun seçin.";
            return;
        }

        PersistTransferColumnOptions();

        var transferRequest = new ExcelTransferRequest
        {
            WorkbookFilePath = SelectedWorkbook.FilePath,
            WorkbookFileName = SelectedWorkbook.DisplayName,
            WorksheetName = sheetName,
            Range = BuildCurrentRange(dataEndRow),
            HeaderTexts = GetHeaderRowTexts(),
            AppliedColumnMappings = selectedOptions.Select(option => new TransferColumnMapping
            {
                SourceColumn = option.SourceColumn,
                FieldName = ResolveLogicalField(option),
                DataType = option.DataType
            }).ToList(),
            WorkingDataColumns = GetCurrentWorksheet()?.WorkingData?.Columns.Select(column => new TransferWorkingColumn
            {
                SourceField = column.SourceField,
                Header = column.Header,
                OriginalSourceColumn = column.OriginalSourceColumn
            }).ToList(),
            TargetElementId = UpdateExistingTable ? _placementExistingTableId : _placementAnchorElementId,
            ExistingTableMode = UpdateExistingTable ? ExistingTableTransferMode.ReplaceColumnsFromSource : null,
            PreferredDataSourceName = string.IsNullOrWhiteSpace(DataSourceName) ? null : DataSourceName
        };

        var placement = new ExcelTransferPlacementRequest
        {
            Transfer = transferRequest,
            DestinationMode = UpdateExistingTable
                ? ExcelTransferDestinationMode.UpdateExistingTable
                : ExcelTransferDestinationMode.CreateNewTable,
            ExistingTableId = _placementExistingTableId,
            AnchorElementId = _placementAnchorElementId,
            TableName = string.IsNullOrWhiteSpace(PlacementTableName) ? NextTableName(report) : PlacementTableName.Trim(),
            IncludeHeading = CreateNewTable && IncludePlacementHeading,
            HeadingText = PlacementHeadingText,
            IncludeAltHeading = CreateNewTable && IncludePlacementAltHeading,
            AltHeadingText = PlacementAltHeadingText,
            Columns = ColumnMappings.Select(option => new TransferColumnSelection
            {
                ProviderField = option.ProviderField,
                LogicalField = ResolveLogicalField(option),
                Header = option.FieldName,
                SemanticRole = option.SemanticRole,
                SourceOrder = option.SourceOrder,
                IsIncluded = option.IsIncluded
            }).ToList()
        };

        var coordinated = ExcelTransferPlacementCoordinator.Transfer(
            _transferService,
            project,
            report,
            placement);
        var result = coordinated.TransferResult;

        switch (result.Outcome)
        {
            case TransferOutcome.Success when result.Table is not null:
                IsTransferPlacementOpen = false;
                _workspace.SetSelectedReportElement(result.Table.Id);
                _workspace.NotifyReportContentChanged();
                StatusText = result.CreatedNewTable
                    ? $"{result.WorksheetName} · {result.RangeReference} → {result.Table.Name} oluşturuldu ve önizlemeye eklendi"
                    : $"{result.WorksheetName} · {result.RangeReference} → {result.Table.Name} güncellendi";
                break;

            case TransferOutcome.RequiresSourceFieldMapping:
                IsTransferPlacementOpen = false;
                OpenSourceFieldMapping(result);
                break;

            case TransferOutcome.RequiresExistingTableDecision:
                StatusText = "Tablo hedefi değişti; Word'e Aktar penceresini yeniden açın.";
                break;

            default:
                StatusText = result.Error ?? "Aktarım tamamlanamadı.";
                break;
        }
    }

    public ColumnMappingRowViewModel? GetOrCreateTransferColumnOption(string columnIdentity)
    {
        EnsureColumnTransferOptions();
        return ColumnMappings.FirstOrDefault(option =>
            string.Equals(option.ProviderField, columnIdentity, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.SourceColumn, columnIdentity, StringComparison.OrdinalIgnoreCase));
    }

    public string ResolveDisplayColumnLetter(string columnIdentity, int displayIndex)
    {
        var option = GetOrCreateTransferColumnOption(columnIdentity);
        if (option is not null)
        {
            try
            {
                _ = ColumnLetterConverter.ToIndex(option.SourceColumn);
                return option.SourceColumn.ToUpperInvariant();
            }
            catch (ArgumentException)
            {
                // Project-only inserted columns keep a stable ProviderField but
                // display their current Excel-style coordinate.
            }
        }

        return ColumnLetterConverter.ToLetters(Math.Max(1, displayIndex + 1));
    }

    public void SetColumnIncluded(string columnIdentity, bool included)
    {
        var option = GetOrCreateTransferColumnOption(columnIdentity);
        if (option is null) return;
        option.IsIncluded = included;
        PersistTransferColumnOptions();
        StatusText = included
            ? $"{ResolveDisplayColumnLetter(columnIdentity, option.SourceOrder)} sütunu aktarıma eklendi"
            : $"{ResolveDisplayColumnLetter(columnIdentity, option.SourceOrder)} sütunu aktarımdan çıkarıldı";
    }

    public async Task CommitGridCellEditAsync(int gridRowIndex, string columnIdentity, string? value)
    {
        if (!IsHeaderGridRow(gridRowIndex))
        {
            await CommitCellEditAsync(NormalizeDataDisplayRowIndex(gridRowIndex), columnIdentity, value);
            return;
        }

        var worksheet = await EnsureWorkingDataAsync();
        if (worksheet?.WorkingData is not { } workingData) return;
        var columnIndex = WorkingDataInteractionResolver.ResolveColumnIndex(workingData, columnIdentity);
        if (columnIndex < 0)
        {
            StatusText = "Başlık sütunu çözülemedi.";
            return;
        }

        var updatedHeader = value?.Trim() ?? string.Empty;
        var result = _workingDataService.Mutate(worksheet, HistoryFor(worksheet), () =>
        {
            workingData.Columns[columnIndex].Header = updatedHeader;
            return Result.Success();
        });
        if (result.IsFailure)
        {
            StatusText = result.Error!;
            return;
        }

        EnsureColumnTransferOptions(forceRebuild: true);
        var option = GetOrCreateTransferColumnOption(columnIdentity);
        if (option is not null)
            option.FieldName = updatedHeader;
        PersistTransferColumnOptions();
        CompleteMutation(worksheet, result, "Sütun başlığı güncellendi · Değiştirildi");
    }

    public async Task ClearGridCellsAsync(IReadOnlyCollection<GridCellTarget> gridCells)
    {
        var headerCells = gridCells.Where(cell => IsHeaderGridRow(cell.DisplayRowIndex)).ToList();
        foreach (var headerCell in headerCells)
            await CommitGridCellEditAsync(headerCell.DisplayRowIndex, headerCell.ColumnIdentity, string.Empty);

        var dataCells = gridCells
            .Where(cell => !IsHeaderGridRow(cell.DisplayRowIndex))
            .Select(cell => new GridCellTarget(NormalizeDataDisplayRowIndex(cell.DisplayRowIndex), cell.ColumnIdentity))
            .ToList();
        if (dataCells.Count > 0)
            await ClearCellsAsync(dataCells);
    }

    public async Task PasteGridClipboardAsync(int gridRowIndex, string columnIdentity, string clipboardText)
    {
        if (IsHeaderGridRow(gridRowIndex))
        {
            if (clipboardText.Contains('\t') || clipboardText.Contains('\n') || clipboardText.Contains('\r'))
            {
                StatusText = "Başlık hücresine tek bir metin değeri yapıştırın.";
                return;
            }
            await CommitGridCellEditAsync(gridRowIndex, columnIdentity, clipboardText);
            return;
        }

        await PasteClipboardAsync(NormalizeDataDisplayRowIndex(gridRowIndex), columnIdentity, clipboardText);
    }

    public async Task InsertGridRowRelativeAsync(int displayRowIndex, bool below)
    {
        if (IsHeaderGridRow(displayRowIndex))
        {
            StatusText = "Başlık satırına göre veri satırı eklenemez; bir veri satırı seçin.";
            return;
        }
        await InsertRowRelativeAsync(NormalizeDataDisplayRowIndex(displayRowIndex), below);
    }

    public async Task DeleteGridRowsAsync(IReadOnlyCollection<int> displayRowIndexes)
    {
        if (displayRowIndexes.Any(IsHeaderGridRow))
        {
            StatusText = "Başlık satırı silinemez.";
            return;
        }
        await DeleteRowsAsync(displayRowIndexes.Select(NormalizeDataDisplayRowIndex).ToList());
    }

    public async Task ClearGridRowsAsync(IReadOnlyCollection<int> displayRowIndexes)
    {
        if (displayRowIndexes.Any(IsHeaderGridRow))
        {
            StatusText = "Başlık satırı satır temizleme işlemiyle silinemez.";
            return;
        }
        await ClearRowsAsync(displayRowIndexes.Select(NormalizeDataDisplayRowIndex).ToList());
    }

    private bool IsHeaderGridRow(int displayRowIndex)
    {
        if (HeaderRowNumber is not { } headerRow
            || displayRowIndex < 0
            || displayRowIndex >= PreviewTable.Rows.Count)
            return false;

        return int.TryParse(PreviewTable.Rows[displayRowIndex]["#"]?.ToString(), out var rowNumber)
               && rowNumber == headerRow;
    }

    private int NormalizeDataDisplayRowIndex(int displayRowIndex) =>
        HasInjectedWorkingHeaderRow() && displayRowIndex > 0 ? displayRowIndex - 1 : displayRowIndex;

    private bool HasInjectedWorkingHeaderRow() =>
        GetCurrentWorksheet()?.WorkingData is not null
        && PreviewTable.Rows.Count > 0
        && IsHeaderGridRow(0);

    private void EnsureColumnTransferOptions(bool forceRebuild = false)
    {
        if (PreviewTable.Columns.Count <= 1) return;

        var identities = PreviewTable.Columns.Cast<DataColumn>()
            .Where(column => !string.Equals(column.ColumnName, "#", StringComparison.Ordinal))
            .Select((column, index) => (Identity: column.ColumnName, Order: index))
            .ToList();

        if (!forceRebuild
            && ColumnMappings.Count == identities.Count
            && identities.All(item => ColumnMappings.Any(option =>
                string.Equals(option.ProviderField, item.Identity, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.SourceColumn, item.Identity, StringComparison.OrdinalIgnoreCase))))
        {
            SupplementPersistedOptionMetadata();
            return;
        }

        var previous = ColumnMappings.ToList();
        var worksheet = GetCurrentWorksheet();
        var workingData = worksheet?.WorkingData;
        var descriptors = identities.Select(item =>
        {
            var workingColumn = workingData?.Columns.FirstOrDefault(column =>
                string.Equals(column.SourceField, item.Identity, StringComparison.OrdinalIgnoreCase));
            var sourceColumn = workingColumn?.OriginalSourceColumn ?? item.Identity;
            var header = workingColumn?.Header ?? ReadHeaderCell(item.Identity);
            var persisted = worksheet?.ColumnMappings.FirstOrDefault(mapping =>
                string.Equals(mapping.SourceColumn, sourceColumn, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapping.SourceColumn, item.Identity, StringComparison.OrdinalIgnoreCase));
            var current = previous.FirstOrDefault(option =>
                string.Equals(option.ProviderField, item.Identity, StringComparison.OrdinalIgnoreCase)
                || string.Equals(option.SourceColumn, sourceColumn, StringComparison.OrdinalIgnoreCase));
            var role = ParseRole(persisted?.SemanticRole)
                       ?? current?.SemanticRole
                       ?? _semanticFieldMatcher.Match(persisted?.DisplayHeader ?? header);
            return new
            {
                item.Identity,
                item.Order,
                SourceColumn = sourceColumn,
                Header = persisted?.DisplayHeader ?? current?.FieldName ?? header,
                Persisted = persisted,
                Current = current,
                Role = role
            };
        }).ToList();

        var hasEnglishPartName = descriptors.Any(item => item.Role == ExcelSemanticFieldRole.PartNameEnglish);
        ColumnMappings.Clear();
        foreach (var descriptor in descriptors)
        {
            var defaultIncluded = descriptor.Role != ExcelSemanticFieldRole.Unknown
                                  && !(descriptor.Role == ExcelSemanticFieldRole.PartNameTurkish && hasEnglishPartName);
            ColumnMappings.Add(new ColumnMappingRowViewModel
            {
                SourceColumn = descriptor.SourceColumn,
                ProviderField = descriptor.Identity,
                FieldName = string.IsNullOrWhiteSpace(descriptor.Header)
                    ? $"Sütun {ColumnLetterConverter.ToLetters(descriptor.Order + 1)}"
                    : descriptor.Header,
                DataType = descriptor.Persisted?.TargetField.DataType ?? descriptor.Current?.DataType ?? "string",
                IsIncluded = descriptor.Persisted?.IsIncluded ?? descriptor.Current?.IsIncluded ?? defaultIncluded,
                SemanticRole = descriptor.Role,
                SourceOrder = descriptor.Order
            });
        }
    }

    private void SupplementPersistedOptionMetadata()
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet is null) return;
        foreach (var option in ColumnMappings)
        {
            var persisted = worksheet.ColumnMappings.FirstOrDefault(mapping =>
                string.Equals(mapping.SourceColumn, option.SourceColumn, StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapping.SourceColumn, option.ProviderField, StringComparison.OrdinalIgnoreCase));
            if (persisted is null) continue;
            option.ProviderField = string.IsNullOrWhiteSpace(option.ProviderField) ? option.SourceColumn : option.ProviderField;
            option.FieldName = persisted.DisplayHeader ?? option.FieldName;
            option.IsIncluded = persisted.IsIncluded;
            option.SemanticRole = ParseRole(persisted.SemanticRole) ?? option.SemanticRole;
        }
    }

    private void PersistTransferColumnOptions()
    {
        var project = _workspace.ActiveProject;
        if (project is null
            || SelectedWorkbook?.SelectedSheetName is not { } sheetName
            || DetectedDataEndRow is not { } dataEndRow)
            return;

        var worksheet = _workingDataService.SaveSelectedRange(
            project,
            SelectedWorkbook.FilePath,
            SelectedWorkbook.DisplayName,
            sheetName,
            BuildCurrentRange(dataEndRow),
            string.IsNullOrWhiteSpace(DataSourceName) ? null : DataSourceName);

        worksheet.ColumnMappings.Clear();
        foreach (var option in ColumnMappings.OrderBy(option => option.SourceOrder))
        {
            worksheet.ColumnMappings.Add(new ColumnMapping
            {
                SourceColumn = option.SourceColumn,
                TargetField = new DataField
                {
                    Name = ResolveLogicalField(option),
                    DataType = option.DataType
                },
                DisplayHeader = option.FieldName,
                IsIncluded = option.IsIncluded,
                SemanticRole = option.SemanticRole.ToString()
            });
        }
        _mappingsApplied = worksheet.ColumnMappings.Count > 0;
    }

    private void InjectWorkingDataHeaderRow(DataTable table)
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet?.WorkingData is not { } workingData || HeaderRowNumber is not { } headerRow)
            return;
        if (table.Rows.Cast<DataRow>().Any(row =>
                int.TryParse(row["#"]?.ToString(), out var rowNumber) && rowNumber == headerRow))
            return;

        var row = table.NewRow();
        row["#"] = headerRow;
        foreach (DataColumn dataColumn in table.Columns)
        {
            if (dataColumn.ColumnName == "#") continue;
            var workingColumn = workingData.Columns.FirstOrDefault(column =>
                string.Equals(column.SourceField, dataColumn.ColumnName, StringComparison.OrdinalIgnoreCase));
            row[dataColumn.ColumnName] = workingColumn?.Header ?? string.Empty;
        }
        table.Rows.InsertAt(row, 0);
    }

    private void ApplyPersistedHeaderOverrides(DataTable table)
    {
        var worksheet = GetCurrentWorksheet();
        if (worksheet is null || HeaderRowNumber is not { } headerRow) return;
        var headerDataRow = table.Rows.Cast<DataRow>().FirstOrDefault(row =>
            int.TryParse(row["#"]?.ToString(), out var rowNumber) && rowNumber == headerRow);
        if (headerDataRow is null) return;

        foreach (var mapping in worksheet.ColumnMappings)
        {
            var columnIdentity = ResolveTableColumnIdentity(mapping.SourceColumn, worksheet.WorkingData);
            if (columnIdentity is null || !table.Columns.Contains(columnIdentity)) continue;
            headerDataRow[columnIdentity] = mapping.DisplayHeader ?? mapping.TargetField.Name;
        }
    }

    private string ReadHeaderCell(string columnIdentity)
    {
        if (HeaderRowNumber is not { } headerRow) return columnIdentity;
        var row = PreviewTable.Rows.Cast<DataRow>().FirstOrDefault(candidate =>
            int.TryParse(candidate["#"]?.ToString(), out var rowNumber) && rowNumber == headerRow);
        return row is not null && PreviewTable.Columns.Contains(columnIdentity)
            ? row[columnIdentity]?.ToString() ?? columnIdentity
            : columnIdentity;
    }

    private static string? ResolveTableColumnIdentity(string sourceColumn, WorksheetWorkingData? workingData)
    {
        if (workingData is null) return sourceColumn;
        return workingData.Columns.FirstOrDefault(column =>
                   string.Equals(column.OriginalSourceColumn, sourceColumn, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(column.SourceField, sourceColumn, StringComparison.OrdinalIgnoreCase))
               ?.SourceField;
    }

    private static ExcelSemanticFieldRole? ParseRole(string? value) =>
        Enum.TryParse<ExcelSemanticFieldRole>(value, ignoreCase: true, out var role) ? role : null;

    private static string ResolveLogicalField(ColumnMappingRowViewModel option) =>
        option.SemanticRole == ExcelSemanticFieldRole.Unknown
            ? (string.IsNullOrWhiteSpace(option.FieldName) ? option.ProviderField : option.FieldName.Trim())
            : option.SemanticRole.ToString();

    private string ResolvePlacementParentText(Report report)
    {
        var root = ReportElementFlattener.Flatten(report)
            .OfType<TextElement>()
            .FirstOrDefault(text => string.Equals(text.Name, "Document Root", StringComparison.Ordinal));
        var text = root?.Content.Text;
        return $"1. {(string.IsNullOrWhiteSpace(text) ? ExcelTransferPlacementCoordinator.DefaultRootHeadingText : text)}";
    }

    private void RefreshPlacementNumbers()
    {
        var report = _workspace.ActiveReport;
        var headingCount = report is null
            ? 0
            : ReportElementFlattener.Flatten(report)
                .OfType<TextElement>()
                .Count(text => HeadingStylePresets.IsHeading(text.Style)
                               && !string.Equals(text.Name, "Document Root", StringComparison.Ordinal));
        var next = headingCount + 1;
        PlacementHeadingNumber = $"1.{next}";
        PlacementAltHeadingNumber = IncludePlacementHeading ? $"1.{next}.1" : $"1.{next}";
    }

    private static string NextTableName(Report report)
    {
        var count = ReportElementFlattener.Flatten(report).OfType<TableElement>().Count();
        return $"Tablo {count + 1}";
    }
}
