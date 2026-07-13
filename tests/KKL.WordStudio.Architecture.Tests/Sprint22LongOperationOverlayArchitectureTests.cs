namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22LongOperationOverlayArchitectureTests
{
    [Fact]
    public void Shell_UsesOneInWindowLoadingOverlayAsInteractionShield()
    {
        var root = SolutionRootLocator.Find();
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var mainViewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "MainViewModel.cs");
        var operationState = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "LongOperationViewModel.cs");

        Assert.Contains("Panel.ZIndex=\"1000\"", shell, StringComparison.Ordinal);
        Assert.Contains("LongOperation.IsBusy", shell, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"True\"", shell, StringComparison.Ordinal);
        Assert.Contains("LongOperation.CancelCommand", shell, StringComparison.Ordinal);
        Assert.Contains("LongOperation.IsCancellable", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<Popup", shell, StringComparison.Ordinal);

        Assert.Contains("LongOperationViewModel.Shared", mainViewModel, StringComparison.Ordinal);
        Assert.Contains("Dictionary<long, ActiveOperation>", operationState, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource", operationState, StringComparison.Ordinal);
        Assert.Contains("İptal isteği alındı", operationState, StringComparison.Ordinal);
    }

    [Fact]
    public void LargePreview_UsesCancellationAndChunkedUiProjection()
    {
        var root = SolutionRootLocator.Find();
        var preview = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewViewModel.cs");

        Assert.Contains("PageProjectionBatchSize = 25", preview, StringComparison.Ordinal);
        Assert.Contains("LongOperationViewModel.Shared", preview, StringComparison.Ordinal);
        Assert.Contains("_renderer.RenderAsync(project, report, operation.Token)", preview, StringComparison.Ordinal);
        Assert.Contains("Task.Run", preview, StringComparison.Ordinal);
        Assert.Contains("operation.Token.ThrowIfCancellationRequested", preview, StringComparison.Ordinal);
        Assert.Contains("await Task.Yield()", preview, StringComparison.Ordinal);
        Assert.Contains("completed % PageProjectionBatchSize", preview, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException)", preview, StringComparison.Ordinal);

        Assert.DoesNotContain("new PreviewRenderer", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportContentBuilder", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument", preview, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcelAndTransferOperations_AreDecoratedAtTheCompositionRoot()
    {
        var root = SolutionRootLocator.Find();
        var app = Read(root, "src", "KKL.WordStudio.UI", "App.xaml.cs");
        var decorators = Read(root, "src", "KKL.WordStudio.UI", "Services", "LongOperationDecorators.cs");

        Assert.Contains("new LongOperationExcelWorkbookReader", app, StringComparison.Ordinal);
        Assert.Contains("new OpenXmlExcelWorkbookReader", app, StringComparison.Ordinal);
        Assert.Contains("new LongOperationExcelReportTransferService", app, StringComparison.Ordinal);
        Assert.Contains("new ColumnSelectionExcelReportTransferService", app, StringComparison.Ordinal);

        Assert.Contains("class LongOperationExcelWorkbookReader : IExcelWorkbookReader", decorators, StringComparison.Ordinal);
        Assert.Contains("class LongOperationExcelReportTransferService : IExcelReportTransferService", decorators, StringComparison.Ordinal);
        Assert.Contains("return await operation(linkedCancellation.Token)", decorators, StringComparison.Ordinal);
        Assert.Contains("var result = _inner.Transfer", decorators, StringComparison.Ordinal);
        Assert.DoesNotContain("SpreadsheetDocument", decorators, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument", decorators, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadingState_PaintsTheShieldBeforeReturningToHeavyWork()
    {
        var root = SolutionRootLocator.Find();
        var operationState = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "LongOperationViewModel.cs");
        var reader = Read(root, "src", "KKL.WordStudio.Infrastructure", "Excel", "OpenXmlExcelWorkbookReader.cs");

        Assert.Contains("FlushPresentationIfAvailable", operationState, StringComparison.Ordinal);
        Assert.Contains("DispatcherFrame", operationState, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.ContextIdle", operationState, StringComparison.Ordinal);
        Assert.Contains("Application.Current?.Dispatcher", operationState, StringComparison.Ordinal);

        Assert.Contains("Task.Run(() => OpenWorkbook", reader, StringComparison.Ordinal);
        Assert.Contains("() => GetSheetPreview", reader, StringComparison.Ordinal);
        Assert.Contains("() => DetectDataRangeCore", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.FromResult(Result.Success(preview))", reader, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
