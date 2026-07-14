namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint24WordExportReadinessArchitectureTests
{
    [Fact]
    public void WordExport_ReusesGroupedControlDiagnosticsAndAppliesSeverityGate()
    {
        var root = SolutionRootLocator.Find();
        var assessment = Read(root, "src", "KKL.WordStudio.Application", "Preview", "ReportReadinessAssessment.cs");
        var main = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "MainViewModel.cs");
        var store = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewDiagnosticsStore.cs");

        Assert.Contains("PreviewDiagnosticGroup", assessment, StringComparison.Ordinal);
        Assert.Contains("PreviewDiagnosticSummaryService.Group", assessment, StringComparison.Ordinal);
        Assert.Contains("ReportReadinessAssessment.FromGroups(DockViewModel.Diagnostics.Groups)", main, StringComparison.Ordinal);
        Assert.Contains("readiness.BlocksExport", main, StringComparison.Ordinal);
        Assert.Contains("readiness.RequiresWarningConfirmation", main, StringComparison.Ordinal);
        Assert.Contains("ReportPaneViewModel.Shared.OpenForAction()", main, StringComparison.Ordinal);
        Assert.Contains("DockViewModel.ShowWarningsCommand.Execute(null)", main, StringComparison.Ordinal);
        Assert.Contains("ShowConfirmation", main, StringComparison.Ordinal);
        Assert.Contains("IReadOnlyList<PreviewDiagnosticGroup> Groups", store, StringComparison.Ordinal);

        Assert.DoesNotContain("new PreviewRenderer", main, StringComparison.Ordinal);
        Assert.DoesNotContain("new ReportValidator", main, StringComparison.Ordinal);
        Assert.DoesNotContain("new WordExporter", main, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
