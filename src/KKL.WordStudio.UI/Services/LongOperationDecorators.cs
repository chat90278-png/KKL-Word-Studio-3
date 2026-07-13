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
/// It never reads workbook bytes itself; it publishes long-operation state and
/// combines the shell cancellation token with the caller's token.
/// </summary>
public sealed class LongOperationExcelWorkbookReader : IExcelWorkbookReader
{
    private readonly IExcelWorkbookReader _inner;
    private readonly LongOperationViewModel _operations;

    public LongOperationExcelWorkbookReader(
        IExcelWorkbookReader inner,
        LongOperationViewModel? operations = null)
    {
        _inner = inner;
        _operations = operations ?? LongOperationViewModel.Shared;
    }

    public Task<Result<Workbook>> OpenWorkbookAsync(
        string filePath,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            "Excel dosyası açılıyor",
            $"{Path.GetFileName(filePath)} · çalışma sayfaları okunuyor…",
            token => _inner.OpenWorkbookAsync(filePath, token),
            cancellationToken);

    public Task<Result<SheetPreview>> GetSheetPreviewAsync(
        string filePath,
        string worksheetName,
        int maxPreviewRows = 100,
        CancellationToken cancellationToken = default) =>
        RunAsync(
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
        string title,
        string detail,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken callerToken)
    {
        using var lease = _operations.Begin(title, detail, isCancellable: true);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            callerToken,
            lease.Token);

        return await operation(linkedCancellation.Token);
    }
}

/// <summary>
/// Transparent presentation decorator for the existing transfer service. It
/// keeps the shell shield visible through the current dispatcher turn so the
/// Preview refresh can take over without a clickable gap.
/// </summary>
public sealed class LongOperationExcelReportTransferService : IExcelReportTransferService
{
    private readonly IExcelReportTransferService _inner;
    private readonly LongOperationViewModel _operations;

    public LongOperationExcelReportTransferService(
        IExcelReportTransferService inner,
        LongOperationViewModel? operations = null)
    {
        _inner = inner;
        _operations = operations ?? LongOperationViewModel.Shared;
    }

    public ExcelTransferResult Transfer(Project project, Report report, ExcelTransferRequest request)
    {
        var lease = _operations.Begin(
            "Word'e aktarılıyor",
            $"{request.WorksheetName} · {request.Range.RangeReference} rapora bağlanıyor…",
            isCancellable: false);

        try
        {
            var result = _inner.Transfer(project, report, request);
            lease.ReportDetail(result.Outcome == TransferOutcome.Success
                ? "Aktarım tamamlandı · rapor önizlemesi yenileniyor…"
                : "Aktarım kararı hazırlanıyor…");
            DisposeAfterCurrentDispatcherTurn(lease);
            return result;
        }
        catch
        {
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
