namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22ResponsiveQuickAssemblyArchitectureTests
{
    [Fact]
    public void QuickAssembly_ExposesProgressCancelAndDuplicateCommandProtection()
    {
        var root = SolutionRootLocator.Find();
        var viewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "QuickAssemblyViewModel.cs");
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "QuickAssemblyView.xaml");
        var orchestrator = Read(root, "src", "KKL.WordStudio.Application", "QuickAssembly", "QuickAssemblyBatchOrchestrator.cs");

        Assert.Contains("CancellationTokenSource", viewModel, StringComparison.Ordinal);
        Assert.Contains("if (IsBusy)", viewModel, StringComparison.Ordinal);
        Assert.Contains("CancelTransferSelectedCommand", viewModel, StringComparison.Ordinal);
        Assert.Contains("ProgressPercent", viewModel, StringComparison.Ordinal);
        Assert.Contains("ProgressBar", view, StringComparison.Ordinal);
        Assert.Contains("Value=\"{Binding ProgressPercent, Mode=OneWay}\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Value=\"{Binding ProgressPercent}\"", view, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelTransferSelectedCommand}\"", view, StringComparison.Ordinal);
        Assert.Contains("Binding IsBusy", view, StringComparison.Ordinal);

        Assert.Contains("IProgress<QuickAssemblyProgress>", orchestrator, StringComparison.Ordinal);
        Assert.Contains("IsCancelled", orchestrator, StringComparison.Ordinal);
        Assert.Contains("Completed target results are always retained", orchestrator, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponsiveBatch_RemainsAnOrchestratorAndDoesNotForkDataOrRenderingPipelines()
    {
        var root = SolutionRootLocator.Find();
        var viewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "QuickAssemblyViewModel.cs");
        var orchestrator = Read(root, "src", "KKL.WordStudio.Application", "QuickAssembly", "QuickAssemblyBatchOrchestrator.cs");

        Assert.Contains("_excelWorkspace.TransferQuickAssemblyTargetAsync", viewModel, StringComparison.Ordinal);
        Assert.Contains("transferSingleTargetAsync", orchestrator, StringComparison.Ordinal);

        Assert.DoesNotContain("IExcelWorkbookReader", orchestrator, StringComparison.Ordinal);
        Assert.DoesNotContain("SpreadsheetDocument", orchestrator, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument", orchestrator, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportContentBuilder", orchestrator, StringComparison.Ordinal);
        Assert.DoesNotContain("Domain.Projects", viewModel, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}