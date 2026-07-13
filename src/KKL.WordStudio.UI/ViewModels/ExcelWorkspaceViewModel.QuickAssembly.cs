namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.QuickAssembly;
using KKL.WordStudio.Application.Transfer;

public sealed partial class ExcelWorkspaceViewModel
{
    private readonly SemaphoreSlim _quickAssemblyTransferGate = new(1, 1);

    /// <summary>
    /// Executes one quick-assembly target through the same transfer service and
    /// current-range/WorkingData contracts used by the normal Word'e Aktar
    /// action. The target is always created as a new table; an existing selected
    /// report table is never silently overwritten by batch assembly.
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

            // Set the sheet first so workbook activation and the explicit await
            // both resolve the same target. The normal selection hook may start
            // an equivalent preview load; the awaited load below is the
            // authoritative completion point for this batch item.
            workbook.SelectedSheetName = target.WorksheetName;
            SelectedWorkbook = workbook;
            await LoadPreviewAsync();

            cancellationToken.ThrowIfCancellationRequested();

            var project = _workspace.ActiveProject;
            var report = _workspace.ActiveReport;
            if (project is null || report is null)
                return CreateQuickAssemblyFailed("Etkin rapor yok — önce bir proje oluşturun veya açın.");

            if (_currentPreview is null || _currentPreview.ColumnCount <= 0)
                return CreateQuickAssemblyFailed("Sayfa önizlemesi yüklenemedi.");

            if (DetectedDataEndRow is not { } dataEndRow || dataEndRow < EffectiveDataStartRow)
                return CreateQuickAssemblyFailed("Veri aralığı algılanamadı veya geçersiz.");

            var request = new ExcelTransferRequest
            {
                WorkbookFilePath = workbook.FilePath,
                WorkbookFileName = workbook.DisplayName,
                WorksheetName = target.WorksheetName,
                Range = BuildCurrentRange(dataEndRow),
                HeaderTexts = GetHeaderRowTexts(),
                AppliedColumnMappings = _mappingsApplied && ColumnMappings.Count > 0
                    ? ColumnMappings.Select(mapping => new TransferColumnMapping
                    {
                        SourceColumn = mapping.SourceColumn,
                        FieldName = mapping.FieldName,
                        DataType = mapping.DataType
                    }).ToList()
                    : null,
                WorkingDataColumns = GetCurrentWorksheet()?.WorkingData?.Columns.Select(column => new TransferWorkingColumn
                {
                    SourceField = column.SourceField,
                    Header = column.Header,
                    OriginalSourceColumn = column.OriginalSourceColumn
                }).ToList(),

                // Batch assembly is deliberately non-destructive. It never
                // routes through the currently selected report element and it
                // does not reuse a possibly stale manually typed source name
                // from another active workbook.
                TargetElementId = null,
                ExistingTableMode = null,
                SourceFieldMappings = null,
                PreferredDataSourceName = null
            };

            var result = _transferService.Transfer(project, report, request);
            if (result.Outcome == TransferOutcome.Success && result.Table is not null)
            {
                if (!result.CreatedNewTable)
                {
                    return CreateQuickAssemblyFailed(
                        "Toplu aktarım yeni tablo oluşturmadığı için güvenlik amacıyla başarı sayılmadı.");
                }

                result.Table.Caption = string.IsNullOrWhiteSpace(target.Caption)
                    ? null
                    : target.Caption.Trim();
                _workspace.SetSelectedReportElement(result.Table.Id);
                _workspace.NotifyReportContentChanged();
                StatusText = $"{target.WorksheetName} · {result.RangeReference} → {result.Table.Name} oluşturuldu";

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
                return CreateQuickAssemblySkipped("Kaynak alan eşlemesi gerektiği için toplu aktarımda atlandı.");

            return CreateQuickAssemblyFailed(result.Error ?? "Sayfa rapora aktarılamadı.");
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
