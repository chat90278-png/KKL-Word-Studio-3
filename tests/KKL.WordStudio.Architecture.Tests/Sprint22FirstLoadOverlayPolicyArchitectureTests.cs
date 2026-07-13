namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22FirstLoadOverlayPolicyArchitectureTests
{
    [Fact]
    public void ExcelOverlay_IsLimitedToTheInitialLoadSequence()
    {
        var root = SolutionRootLocator.Find();
        var policy = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "LongOperationDisplayPolicy.cs");
        var decorators = Read(root, "src", "KKL.WordStudio.UI", "Services", "LongOperationDecorators.cs");

        Assert.Contains("ExcelOverlayStage.OpenWorkbook", decorators, StringComparison.Ordinal);
        Assert.Contains("ExcelOverlayStage.SheetPreview", decorators, StringComparison.Ordinal);
        Assert.Contains("ExcelOverlayStage.DetectRange", decorators, StringComparison.Ordinal);
        Assert.Contains("ExcelOverlayStage.WorkingData", decorators, StringComparison.Ordinal);
        Assert.Contains("if (!_displayPolicy.ShouldShowExcelOverlay(stage))", decorators, StringComparison.Ordinal);

        Assert.Contains("_initialExcelLoadCompleted = true", policy, StringComparison.Ordinal);
        Assert.Contains("_initialSheetPreviewCount > 1", policy, StringComparison.Ordinal);
        Assert.Contains("case ExcelOverlayStage.WorkingData", policy, StringComparison.Ordinal);
        Assert.Contains("return false", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void WordOverlay_IsLimitedToTheFirstSuccessfulTransferAndItsPreview()
    {
        var root = SolutionRootLocator.Find();
        var policy = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "LongOperationDisplayPolicy.cs");
        var decorators = Read(root, "src", "KKL.WordStudio.UI", "Services", "LongOperationDecorators.cs");
        var operationState = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "LongOperationViewModel.cs");

        Assert.Contains("TryBeginFirstWordTransfer", decorators, StringComparison.Ordinal);
        Assert.Contains("CompleteFirstWordTransfer(succeeded)", decorators, StringComparison.Ordinal);
        Assert.Contains("result.Outcome == TransferOutcome.Success", decorators, StringComparison.Ordinal);
        Assert.Contains("_firstWordTransferPreviewPending = true", policy, StringComparison.Ordinal);
        Assert.Contains("TryConsumeFirstWordTransferPreview", operationState, StringComparison.Ordinal);
        Assert.Contains("ReportPreviewTitle", operationState, StringComparison.Ordinal);
    }

    [Fact]
    public void LaterPreviewRefreshes_StayHiddenButCancelStaleWork()
    {
        var root = SolutionRootLocator.Find();
        var operationState = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "LongOperationViewModel.cs");
        var preview = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewViewModel.cs");

        Assert.Contains("BeginHiddenPreview", operationState, StringComparison.Ordinal);
        Assert.Contains("_hiddenPreviewCancellation", operationState, StringComparison.Ordinal);
        Assert.Contains("previous?.Cancel()", operationState, StringComparison.Ordinal);
        Assert.Contains("EndHiddenPreview", operationState, StringComparison.Ordinal);

        Assert.Contains("_renderer.RenderAsync(project, report, operation.Token)", preview, StringComparison.Ordinal);
        Assert.Contains("PageProjectionBatchSize = 25", preview, StringComparison.Ordinal);
        Assert.Contains("generation != _refreshGeneration", preview, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
