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
            {
                return Skipped($"'{target.WorkbookDisplayName}' artık yüklü değil.");
            }

            if (!workbook.SheetNames.Contains(target.WorksheetName))
            {
                return Skipped($"'{target.WorksheetName}' sayfası artık kaynakta bulunmuyor.");
            }

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
                return Failed("Etkin rapor yok — önce bir proje oluşturun veya açın.");

            if (_currentPreview is null || _currentPreview.ColumnCount <= 0)
                return Failed("Sayfa önizlemesi yüklenemedi.");

            if (DetectedDataEndRow is not { } dataEndRow || dataEndRow < EffectiveDataStartRow)
                return Failed("Veri aralığı algılanamadı veya geçersiz.");

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
                // routes through the currently selected report element.
                TargetElementId = null,
                ExistingTableMode = null,
                SourceFieldMappings = null,
                PreferredDataSourceName = string.IsNullOrWhiteSpace(DataSourceName) ? null : DataSourceName
            };

            var result = _transferService.Transfer(project, report, request);
            if (result.Outcome == TransferOutcome.Success && result.Table is not null)
            {
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
            {
                return Skipped("Mevcut tablo kararı gerektiği için güvenli biçimde atlandı.");
            }

            if (result.Outcome == TransferOutcome.RequiresSourceFieldMapping)
            {
                return Skipped("Kaynak alan eşlemesi gerektiği için toplu aktarımda atlandı.");
            }

            return Failed(result.Error ?? "Sayfa rapora aktarılamadı.");
        }
        finally
        {
            _quickAssemblyTransferGate.Release();
        }
    }

    private static QuickAssemblyTransferOutcome Skipped(string message) => new()
    {
        Status = QuickAssemblyTransferStatus.Skipped,
        Message = message
    };

    private static QuickAssemblyTransferOutcome Failed(string message) => new()
    {
        Status = QuickAssemblyTransferStatus.Failed,
        Message = message
    };
}
