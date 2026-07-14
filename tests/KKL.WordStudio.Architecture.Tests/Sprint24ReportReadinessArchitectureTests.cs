namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint24ReportReadinessArchitectureTests
{
    [Fact]
    public void ReportControl_ReusesPreviewDiagnosticsAndGatesWordExportBySeverity()
    {
        var root = SolutionRootLocator.Find();
        var consolidator = Read(root, "src", "KKL.WordStudio.Application", "Preview", "PreviewDiagnosticConsolidator.cs");
        var store = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewDiagnosticsStore.cs");
        var warningVm = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "WarningCenterViewModel.cs");
        var warningView = Read(root, "src", "KKL.WordStudio.UI", "Views", "WarningCenterView.xaml");
        var shell = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "MainViewModel.cs");
        var dock = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContextDockView.xaml");

        Assert.Contains("PreviewDiagnosticConsolidator.Consolidate", store, StringComparison.Ordinal);
        Assert.Contains("OccurrenceCount", consolidator, StringComparison.Ordinal);
        Assert.Contains("ReportReadinessAssessment.From", shell, StringComparison.Ordinal);
        Assert.Contains("readiness.BlocksExport", shell, StringComparison.Ordinal);
        Assert.Contains("readiness.RequiresWarningConfirmation", shell, StringComparison.Ordinal);
        Assert.Contains("DockViewModel.ShowWarningsCommand.Execute", shell, StringComparison.Ordinal);
        Assert.Contains("SeverityText", warningVm, StringComparison.Ordinal);
        Assert.Contains("OccurrenceText", warningVm, StringComparison.Ordinal);
        Assert.Contains("Text=\"Kontrol\"", dock, StringComparison.Ordinal);
        Assert.Contains("Text=\"Word'e hazır\"", warningView, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding DataContext.NavigateCommand", warningView, StringComparison.Ordinal);

        Assert.DoesNotContain("new PreviewRenderer", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("new ReportValidator", shell, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
