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

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
