namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint24QuickReportStructureArchitectureTests
{
    [Fact]
    public void QuickReportSelection_OwnsClickOrderEditableStructureAndAnchorReferencesOnlyInApplicationSessionState()
    {
        var root = SolutionRootLocator.Find();
        var selection = Read(root, "src", "KKL.WordStudio.Application", "QuickAssembly", "QuickAssemblySelection.cs");
        var domainRoot = Path.Combine(root, "src", "KKL.WordStudio.Domain");
        var domain = string.Join(Environment.NewLine,
            Directory.EnumerateFiles(domainRoot, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.Contains("SelectionOrder", selection, StringComparison.Ordinal);
        Assert.Contains("IncludeHeading", selection, StringComparison.Ordinal);
        Assert.Contains("HeadingText", selection, StringComparison.Ordinal);
        Assert.Contains("IncludeAltHeading", selection, StringComparison.Ordinal);
        Assert.Contains("AltHeadingText", selection, StringComparison.Ordinal);
        Assert.Contains("TableName", selection, StringComparison.Ordinal);
        Assert.Contains("PlacementAnchorKind", selection, StringComparison.Ordinal);
        Assert.Contains("ExistingPlacementAnchorId", selection, StringComparison.Ordinal);
        Assert.Contains("SourcePlacementTargetKey", selection, StringComparison.Ordinal);
        Assert.Contains("RequiredPlacementAnchorKind", selection, StringComparison.Ordinal);
        Assert.Contains("SetTargetSelected", selection, StringComparison.Ordinal);
        Assert.Contains("MoveSelected", selection, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectionOrder", domain, StringComparison.Ordinal);
        Assert.DoesNotContain("QuickAssemblyTarget", domain, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickReportPanel_ShowsSourceSelectionOrderedBlocksAndLevelSpecificParentSelectors()
    {
        var root = SolutionRootLocator.Find();
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "QuickAssemblyView.xaml");
        var viewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "QuickAssemblyViewModel.cs");

        Assert.Contains("1 · KAYNAK SAYFALARI", view, StringComparison.Ordinal);
        Assert.Contains("2 · RAPOR YAPISI", view, StringComparison.Ordinal);
        Assert.Contains("OrderedSelectedSheets", view, StringComparison.Ordinal);
        Assert.Contains("IncludeHeading", view, StringComparison.Ordinal);
        Assert.Contains("HeadingText", view, StringComparison.Ordinal);
        Assert.Contains("IncludeAltHeading", view, StringComparison.Ordinal);
        Assert.Contains("AltHeadingText", view, StringComparison.Ordinal);
        Assert.Contains("TableName", view, StringComparison.Ordinal);
        Assert.Contains("PlacementTargetLabel", view, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding PlacementTargets}\"", view, StringComparison.Ordinal);
        Assert.Contains("SelectedPlacementTarget", view, StringComparison.Ordinal);
        Assert.Contains("MoveEarlierCommand", view, StringComparison.Ordinal);
        Assert.Contains("MoveLaterCommand", view, StringComparison.Ordinal);
        Assert.Contains("OrderBy(sheet => sheet.SelectionOrder", viewModel, StringComparison.Ordinal);
        Assert.Contains(".Where(anchor => anchor.Kind == requiredKind)", viewModel, StringComparison.Ordinal);
        Assert.Contains("ordered.Take", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickReportTransfer_UsesNormalPlacementCoordinatorAndResolvesRequiredOutlineLevel()
    {
        var root = SolutionRootLocator.Find();
        var quick = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ExcelWorkspaceViewModel.QuickAssembly.cs");
        var quickVm = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "QuickAssemblyViewModel.cs");
        var normal = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ExcelWorkspaceViewModel.TransferPlacement.cs");
        var coordinator = Read(root, "src", "KKL.WordStudio.Application", "Transfer", "ExcelTransferPlacementCoordinator.cs");

        Assert.Contains("ExcelTransferPlacementCoordinator.Transfer", quick, StringComparison.Ordinal);
        Assert.Contains("ExcelTransferPlacementCoordinator.Transfer", normal, StringComparison.Ordinal);
        Assert.Contains("target.ResolvedPlacementAnchorId", quick, StringComparison.Ordinal);
        Assert.Contains("RequiredAnchorKind = requiredAnchorKind", quick, StringComparison.Ordinal);
        Assert.Contains("_workspace.SetSelectedReportElement(result.Table.Id)", quick, StringComparison.Ordinal);
        Assert.Contains("IncludeHeading = target.IncludeHeading", quick, StringComparison.Ordinal);
        Assert.Contains("IncludeAltHeading = target.IncludeAltHeading", quick, StringComparison.Ordinal);
        Assert.Contains("TableName = string.IsNullOrWhiteSpace(target.TableName)", quick, StringComparison.Ordinal);
        Assert.Contains("source?.CreatedHeadingElementId", quickVm, StringComparison.Ordinal);
        Assert.Contains("source?.CreatedAltHeadingElementId", quickVm, StringComparison.Ordinal);
        Assert.Contains("MatchesRequiredAnchor", coordinator, StringComparison.Ordinal);
        Assert.Contains("FindAnchorBlockEnd", coordinator, StringComparison.Ordinal);
        Assert.Contains("MoveTableToAnchorBlockEnd", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("new ReportStructureService", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenXmlExcelWorkbookReader", quick, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
