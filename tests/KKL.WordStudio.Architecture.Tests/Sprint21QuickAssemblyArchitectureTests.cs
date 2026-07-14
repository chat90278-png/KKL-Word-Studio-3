namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint21QuickAssemblyArchitectureTests
{
    [Fact]
    public void LoadedSources_HostsCompactSessionOnlyQuickAssemblySurface()
    {
        var root = SolutionRootLocator.Find();
        var loadedSources = Read(root, "src", "KKL.WordStudio.UI", "Views", "LoadedSourcesView.xaml");
        var quickView = Read(root, "src", "KKL.WordStudio.UI", "Views", "QuickAssemblyView.xaml");
        var quickViewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "QuickAssemblyViewModel.cs");
        var app = Read(root, "src", "KKL.WordStudio.UI", "App.xaml.cs");

        Assert.Contains("Text=\"Hızlı Rapor\"", loadedSources, StringComparison.Ordinal);
        Assert.Contains("QuickAssemblyPopup", loadedSources, StringComparison.Ordinal);
        Assert.Contains("Tam rapor yapısı oluştur", quickView, StringComparison.Ordinal);
        Assert.Contains("Rapor Yapısını Oluştur", quickView, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Sources}\"", quickView, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding OrderedSelectedSheets}\"", quickView, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding HeadingText", quickView, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding AltHeadingText", quickView, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding TableName", quickView, StringComparison.Ordinal);
        Assert.Contains("QuickAssemblySelection", quickViewModel, StringComparison.Ordinal);
        Assert.Contains("QuickAssemblyBatchOrchestrator", quickViewModel, StringComparison.Ordinal);
        Assert.Contains("_excelWorkspace.OpenWorkbooks", quickViewModel, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<QuickAssemblyViewModel>()", app, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<QuickAssemblyView>()", app, StringComparison.Ordinal);

        Assert.DoesNotContain("Domain.Projects", quickViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("IExcelWorkbookReader", quickViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument", quickViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchTransfer_ReusesExistingTransferServiceAndNeverOverwritesSelectedTable()
    {
        var root = SolutionRootLocator.Find();
        var excelBatch = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ExcelWorkspaceViewModel.QuickAssembly.cs");
        var orchestrator = Read(root, "src", "KKL.WordStudio.Application", "QuickAssembly", "QuickAssemblyBatchOrchestrator.cs");

        Assert.Contains("ExcelTransferPlacementCoordinator.Transfer", excelBatch, StringComparison.Ordinal);
        Assert.Contains("_transferService", excelBatch, StringComparison.Ordinal);
        Assert.Contains("BuildCurrentRange", excelBatch, StringComparison.Ordinal);
        Assert.Contains("GetHeaderRowTexts", excelBatch, StringComparison.Ordinal);
        Assert.Contains("WorkingDataColumns", excelBatch, StringComparison.Ordinal);
        Assert.Contains("DestinationMode = ExcelTransferDestinationMode.CreateNewTable", excelBatch, StringComparison.Ordinal);
        Assert.Contains("ExistingTableId = null", excelBatch, StringComparison.Ordinal);
        Assert.Contains("AnchorElementId = _workspace.SelectedReportElementId", excelBatch, StringComparison.Ordinal);
        Assert.Contains("result.CreatedNewTable", excelBatch, StringComparison.Ordinal);
        Assert.Contains("transferSingleTargetAsync", orchestrator, StringComparison.Ordinal);
        Assert.Contains("SelectionOrder ?? int.MaxValue", orchestrator, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", orchestrator, StringComparison.Ordinal);

        Assert.DoesNotContain("new OpenXmlExcelWorkbookReader", excelBatch, StringComparison.Ordinal);
        Assert.DoesNotContain("IExcelWorkbookReader", orchestrator, StringComparison.Ordinal);
        Assert.DoesNotContain("TableElement", orchestrator, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument", excelBatch, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickAssemblyState_IsNotPersistedIntoDomain()
    {
        var root = SolutionRootLocator.Find();
        var domainRoot = Path.Combine(root, "src", "KKL.WordStudio.Domain");
        var domainText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(domainRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("QuickAssembly", domainText, StringComparison.Ordinal);
        Assert.DoesNotContain("Hızlı Rapor", domainText, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
