namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint24WordExportPreflightArchitectureTests
{
    [Fact]
    public void WordExport_PreflightsStructuredGroupsBeforeFileDialogAndExporter()
    {
        var root = SolutionRootLocator.Find();
        var mainViewModel = Read(
            root,
            "src",
            "KKL.WordStudio.UI",
            "ViewModels",
            "MainViewModel.cs");
        var dockViewModel = Read(
            root,
            "src",
            "KKL.WordStudio.UI",
            "ViewModels",
            "DockViewModel.cs");
        var warningFilter = Read(
            root,
            "src",
            "KKL.WordStudio.UI",
            "ViewModels",
            "WarningCenterViewModel.ExportPreflight.cs");
        var policy = Read(
            root,
            "src",
            "KKL.WordStudio.Application",
            "Preview",
            "WordExportPreflightPolicy.cs");

        var evaluationIndex = mainViewModel.IndexOf(
            "WordExportPreflightPolicy.Evaluate(DockViewModel.Diagnostics.Groups)",
            StringComparison.Ordinal);
        var blockedIndex = mainViewModel.IndexOf(
            "if (!preflight.CanExport)",
            StringComparison.Ordinal);
        var saveDialogIndex = mainViewModel.IndexOf(
            "SaveWordFile(report.Name)",
            StringComparison.Ordinal);
        var exporterIndex = mainViewModel.IndexOf(
            "exporter.ExportAsync",
            StringComparison.Ordinal);

        Assert.True(evaluationIndex >= 0);
        Assert.True(blockedIndex > evaluationIndex);
        Assert.True(saveDialogIndex > blockedIndex);
        Assert.True(exporterIndex > saveDialogIndex);

        Assert.Contains("ReportPaneViewModel.Shared.OpenForAction()", mainViewModel, StringComparison.Ordinal);
        Assert.Contains("DockViewModel.ShowBlockingErrors()", mainViewModel, StringComparison.Ordinal);
        Assert.Contains("preflight.NonBlockingFindingCount", mainViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("WarningCenterViewModel warningCenterViewModel", mainViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox", mainViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewDiagnosticCatalog.Resolve", mainViewModel, StringComparison.Ordinal);

        Assert.Contains("Show(DockPage.Warnings)", dockViewModel, StringComparison.Ordinal);
        Assert.Contains("BlockingErrorsRequested?.Invoke()", dockViewModel, StringComparison.Ordinal);
        Assert.Contains("BlockingErrorsRequested += ShowErrorsForExportBlock", warningFilter, StringComparison.Ordinal);
        Assert.Contains("Filter = WarningCenterFilter.Error", warningFilter, StringComparison.Ordinal);
        Assert.DoesNotContain("new WarningCenter", warningFilter, StringComparison.Ordinal);

        Assert.Contains("IEnumerable<PreviewDiagnosticGroup>", policy, StringComparison.Ordinal);
        Assert.Contains("PreviewDiagnosticSeverity.Error", policy, StringComparison.Ordinal);
        Assert.Contains("WordExportPreflightStatus.Blocked", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("group.Message", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewDiagnosticCatalog.Resolve", policy, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
