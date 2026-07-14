namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.QuickAssembly;
using KKL.WordStudio.Application.Transfer;

public sealed partial class ExcelWorkspaceViewModel
{
    private readonly SemaphoreSlim _quickAssemblyTransferGate = new(1, 1);

    /// <summary>
    /// Executes one quick-report target through the exact placement coordinator
    /// used by normal Word'e Aktar. The current selected report element is the
    /// first anchor; each successful table becomes the next anchor, so the batch
    /// remains sequential and follows user click order without a second report tree.
    /// </summary>
    public async Task<QuickAssemblyTransferOutcome> TransferQuickAssemblyTargetAsync(
        QuickAssemblyTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        await _quickAssemblyTransferGate.WaitAsync(cancellationToken);
        try
        {
            var workbook = OpenWorkbooks.FirstOrDefault(candidate =>
                string.Equals(candidate.FilePath, target.SourcePath, StringComparison.OrdinalIgnoreCase));
            if (workbook is null)
                return CreateQuickAssemblySkipped($"'{target.WorkbookDisplayName}' artık yüklü değil.");

            if (!workbook.SheetNames.Contains(target.WorksheetName))
                return CreateQuickAssemblySkipped($"'{target.WorksheetName}' sayfası artık kaynakta bulunmuyor.");

            cancellationToken.ThrowIfCancellationRequested();

            // A failed sheet read must never leave the previous target's preview
            // eligible for transfer. Clear the authoritative state before the
            // selection hooks and explicit awaited load run.
            _currentPreview = null;
            DetectedDataEndRow = null;
            workbook.SelectedSheetName = target.WorksheetName;
            SelectedWorkbook = workbook;
            await LoadPreviewAsync();

            cancellationToken.ThrowIfCancellationRequested();

            if (!ReferenceEquals(SelectedWorkbook, workbook)
                || !string.Equals(SelectedWorkbook.SelectedSheetName, target.WorksheetName, StringComparison.Ordinal))
            {
                return CreateQuickAssemblyFailed("Hızlı rapor hedefi yükleme sırasında değişti.");
            }

            var project = _workspace.ActiveProject;
            var report = _workspace.ActiveReport;
            if (project is null || report is null)
                return CreateQuickAssemblyFailed("Rapor çalışma alanı hazır değil.");

            if (_currentPreview is null || _currentPreview.ColumnCount <= 0)
                return CreateQuickAssemblyFailed("Sayfa önizlemesi yüklenemedi.");

            if (DetectedDataEndRow is not { } dataEndRow || dataEndRow < EffectiveDataStartRow)
                return CreateQuickAssemblyFailed("Veri aralığı algılanamadı veya geçersiz.");

            EnsureColumnTransferOptions();
            var selectedOptions = ColumnMappings
                .Where(option => option.IsIncluded)
                .OrderBy(option => option.SourceOrder)
                .ToList();
            if (selectedOptions.Count == 0)
                return CreateQuickAssemblySkipped("Aktarılacak en az bir sütun seçilmediği için yapı atlandı.");

            var transferRequest = new ExcelTransferRequest
            {
                WorkbookFilePath = workbook.FilePath,
                WorkbookFileName = workbook.DisplayName,
                WorksheetName = target.WorksheetName,
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
                TargetElementId = _workspace.SelectedReportElementId,
                ExistingTableMode = null,
                SourceFieldMappings = null,
                PreferredDataSourceName = null
            };

            var placement = new ExcelTransferPlacementRequest
            {
                Transfer = transferRequest,
                DestinationMode = ExcelTransferDestinationMode.CreateNewTable,
                ExistingTableId = null,
                AnchorElementId = _workspace.SelectedReportElementId,
                TableName = string.IsNullOrWhiteSpace(target.TableName)
                    ? NextTableName(report)
                    : target.TableName.Trim(),
                IncludeHeading = target.IncludeHeading,
                HeadingText = target.HeadingText,
                IncludeAltHeading = target.IncludeAltHeading,
                AltHeadingText = target.AltHeadingText,
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

            if (result.Outcome == TransferOutcome.Success && result.Table is not null)
            {
                if (!result.CreatedNewTable)
                {
                    return CreateQuickAssemblyFailed(
                        "Hızlı rapor yeni tablo oluşturmadığı için güvenlik amacıyla başarı sayılmadı.");
                }

                _workspace.SetSelectedReportElement(result.Table.Id);
                _workspace.NotifyReportContentChanged();
                StatusText = $"{target.WorksheetName} · {result.RangeReference} → {result.Table.Name} yapısı oluşturuldu ve önizlemeye eklendi";

                return new QuickAssemblyTransferOutcome
                {
                    Status = QuickAssemblyTransferStatus.Created,
                    Message = StatusText,
                    CreatedElementId = result.Table.Id
                };
            }

            if (result.Outcome == TransferOutcome.RequiresExistingTableDecision)
                return CreateQuickAssemblySkipped("Mevcut tablo kararı gerektiği için güvenli biçimde atlandı.");

            if (result.Outcome == TransferOutcome.RequiresSourceFieldMapping)
                return CreateQuickAssemblySkipped("Kaynak alan eşlemesi gerektiği için hızlı raporda atlandı.");

            return CreateQuickAssemblyFailed(result.Error ?? "Sayfa rapor yapısına aktarılamadı.");
        }
        finally
        {
            _quickAssemblyTransferGate.Release();
        }
    }

    private static QuickAssemblyTransferOutcome CreateQuickAssemblySkipped(string message) => new()
    {
        Status = QuickAssemblyTransferStatus.Skipped,
        Message = message
    };

    private static QuickAssemblyTransferOutcome CreateQuickAssemblyFailed(string message) => new()
    {
        Status = QuickAssemblyTransferStatus.Failed,
        Message = message
    };
}
