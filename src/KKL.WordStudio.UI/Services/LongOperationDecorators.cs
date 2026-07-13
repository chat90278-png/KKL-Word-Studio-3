namespace KKL.WordStudio.UI.Services;

using System.IO;
using System.Windows;
using System.Windows.Threading;
using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Shared.Results;
using KKL.WordStudio.UI.ViewModels;

/// <summary>
/// Presentation-only decorator over the single Infrastructure Excel reader.
/// It never reads workbook bytes itself. The full-shell shield is shown only
/// for the first workbook -> preview -> AutoRange sequence in the session;
/// later sheet changes and WorkingData refreshes remain background operations.
/// </summary>
public sealed class LongOperationExcelWorkbookReader : IExcelWorkbookReader
{
    private readonly IExcelWorkbookReader _inner;
    private readonly LongOperationViewModel _operations;
    private readonly LongOperationDisplayPolicy _displayPolicy;

    public LongOperationExcelWorkbookReader(
        IExcelWorkbookReader inner,
        LongOperationViewModel? operations = null,
        LongOperationDisplayPolicy? displayPolicy = null)
    {
        _inner = inner;
        _operations = operations ?? LongOperationViewModel.Shared;
        _displayPolicy = displayPolicy ?? LongOperationDisplayPolicy.Shared;
    }

    public Task<Result<Workbook>> OpenWorkbookAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            ExcelOverlayStage.OpenWorkbook,
            "Excel dosyası açılıyor",
            $"{Path.GetFileName(filePath)} · çalışma sayfaları okunuyor…",
            token => _inner.OpenWorkbookAsync(filePath, token),
            cancellationToken);

    public Task<Result<SheetPreview>> GetSheetPreviewAsync(
        string filePath,
        string worksheetName,
        int maxPreviewRows = int.MaxValue,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            ExcelOverlayStage.SheetPreview,
            "Excel önizlemesi hazırlanıyor",
            $"{Path.GetFileName(filePath)} · {worksheetName} sayfası okunuyor…",
            token => _inner.GetSheetPreviewAsync(filePath, worksheetName, maxPreviewRows, token),
            cancellationToken);

    public Task<Result<WorksheetWorkingData>> ReadWorkingDataAsync(
        string filePath,
        string worksheetName,
        DataRange range,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            ExcelOverlayStage.WorkingData,
            "Excel çalışma verisi hazırlanıyor",
            $"{worksheetName} · {range.RangeReference} aralığı okunuyor…",
            token => _inner.ReadWorkingDataAsync(filePath, worksheetName, range, token),
            cancellationToken);

    public Task<Result<DataRange>> DetectDataRangeAsync(
        string filePath,
        string worksheetName,
        int dataStartRow,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            ExcelOverlayStage.DetectRange,
            "Excel veri aralığı algılanıyor",
            $"{worksheetName} · {dataStartRow}. satırdan itibaren taranıyor…",
            token => _inner.DetectDataRangeAsync(filePath, worksheetName, dataStartRow, token),
            cancellationToken);

    public Task<Result<DataRange>> DetectDataRangeAsync(
        string filePath,
        string worksheetName,
        int dataStartRow,
        int startColumn,
        int endColumn,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            ExcelOverlayStage.DetectRange,
            "Excel veri aralığı algılanıyor",
            $"{worksheetName} · {dataStartRow}. satırdan itibaren taranıyor…",
            token => _inner.DetectDataRangeAsync(
                filePath,
                worksheetName,
                dataStartRow,
                startColumn,
                endColumn,
                token),
            cancellationToken);

    private async Task<Result<T>> RunAsync<T>(
        ExcelOverlayStage stage,
        string title,
        string detail,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken callerToken)
    {
        if (!_displayPolicy.ShouldShowExcelOverlay(stage))
            return await operation(callerToken);

        // Existing Excel Workspace calls use the default token and do not yet
        // own an OperationCanceledException boundary. They still receive the
        // first-load shield, but cancel is enabled only when the caller owns it.
        using var lease = _operations.Begin(
            title,
            detail,
            isCancellable: callerToken.CanBeCanceled);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            callerToken,
            lease.Token);

        return await operation(linkedCancellation.Token);
    }
}

/// <summary>
/// Transparent presentation decorator for the existing transfer service. Only
/// the first successful Word transfer gets the full-screen shield; later report
/// edits and re-transfers keep using the same engine without blocking chrome.
/// </summary>
public sealed class LongOperationExcelReportTransferService : IExcelReportTransferService
{
    private readonly IExcelReportTransferService _inner;
    private readonly LongOperationViewModel _operations;
    private readonly LongOperationDisplayPolicy _displayPolicy;

    public LongOperationExcelReportTransferService(
        IExcelReportTransferService inner,
        LongOperationViewModel? operations = null,
        LongOperationDisplayPolicy? displayPolicy = null)
    {
        _inner = inner;
        _operations = operations ?? LongOperationViewModel.Shared;
        _displayPolicy = displayPolicy ?? LongOperationDisplayPolicy.Shared;
    }

    public ExcelTransferResult Transfer(Project project, Report report, ExcelTransferRequest request)
    {
        if (!_displayPolicy.TryBeginFirstWordTransfer())
            return _inner.Transfer(project, report, request);

        var lease = _operations.Begin(
            "Word'e aktarılıyor",
            $"{request.WorksheetName} · {request.Range.RangeReference} rapora bağlanıyor…",
            isCancellable: false);

        try
        {
            var result = _inner.Transfer(project, report, request);
            var succeeded = result.Outcome == TransferOutcome.Success;
            _displayPolicy.CompleteFirstWordTransfer(succeeded);

            if (!succeeded)
            {
                lease.Dispose();
                return result;
            }

            lease.ReportDetail("Aktarım tamamlandı · ilk rapor önizlemesi yenileniyor…");
            DisposeAfterCurrentDispatcherTurn(lease);
            return result;
        }
        catch
        {
            _displayPolicy.CompleteFirstWordTransfer(succeeded: false);
            lease.Dispose();
            throw;
        }
    }

    private static void DisposeAfterCurrentDispatcherTurn(LongOperationLease lease)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || !dispatcher.CheckAccess())
        {
            lease.Dispose();
            return;
        }

        dispatcher.BeginInvoke(
            new Action(lease.Dispose),
            DispatcherPriority.Background);
    }
}
