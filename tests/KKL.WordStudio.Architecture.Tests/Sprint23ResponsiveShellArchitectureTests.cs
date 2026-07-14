namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint23ResponsiveShellArchitectureTests
{
    [Fact]
    public void Shell_GroupsPreviewAndContextDockInsideOneCollapsibleReportPane()
    {
        var root = SolutionRootLocator.Find();
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var shellCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");

        Assert.Contains("x:Name=\"ReportPaneShell\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReportPaneColumn\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ReportPaneToggleButton\"", shell, StringComparison.Ordinal);
        Assert.Contains("Click=\"ReportPaneToggleButton_Click\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PreviewHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ContextDockHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("ApplyReportPaneState", shellCode, StringComparison.Ordinal);
        Assert.Contains("ReportPaneShell.Visibility = Visibility.Collapsed", shellCode, StringComparison.Ordinal);
        Assert.Contains("ReportPaneColumn.Width = new GridLength(0)", shellCode, StringComparison.Ordinal);
        Assert.DoesNotContain("DoubleAnimation", shellCode, StringComparison.Ordinal);
        Assert.DoesNotContain("<ColumnDefinition Width=\"1.16*\"", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportPaneState_IsResponsiveSessionStateWithoutPreviewRebuildLogic()
    {
        var root = SolutionRootLocator.Find();
        var state = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ReportPaneViewModel.cs");

        Assert.Contains("SmallViewportThreshold = 1180", state, StringComparison.Ordinal);
        Assert.Contains("WideViewportThreshold = 1500", state, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(viewportWidth * 0.56", state, StringComparison.Ordinal);
        Assert.Contains("OpenForAction", state, StringComparison.Ordinal);
        Assert.DoesNotContain("NotifyReportContentChanged", state, StringComparison.Ordinal);
        Assert.DoesNotContain("PreviewRenderer", state, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulTransferAndContentsNavigation_RevealReportPane()
    {
        var root = SolutionRootLocator.Find();
        var shellCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");
        var contents = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContentsView.xaml.cs");

        Assert.Contains("_excelWorkspaceViewModel.PropertyChanged += ExcelWorkspaceViewModel_PropertyChanged", shellCode, StringComparison.Ordinal);
        Assert.Contains("status.Contains('→')", shellCode, StringComparison.Ordinal);
        Assert.Contains("_viewModel.ReportPane.OpenForAction()", shellCode, StringComparison.Ordinal);
        Assert.Contains("ReportPaneViewModel.Shared.OpenForAction()", contents, StringComparison.Ordinal);
        Assert.Contains("_previewViewModel.NavigateToElement(node.ElementId)", contents, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcelGrid_ProvidesZoomSearchAndRecyclingVirtualization()
    {
        var root = SolutionRootLocator.Find();
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.xaml");
        var tools = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.ResponsiveTools.cs");

        Assert.Contains("x:Name=\"SearchStrip\"", view, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"ZoomPercentText\"", view, StringComparison.Ordinal);
        Assert.Contains("VirtualizationMode=\"Recycling\"", view, StringComparison.Ordinal);
        Assert.Contains("control && e.Key == Key.F", tools, StringComparison.Ordinal);
        Assert.Contains("StringComparison.CurrentCultureIgnoreCase", tools, StringComparison.Ordinal);
        Assert.Contains("% _gridSearchHits.Count", tools, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(percent, 50, 200)", tools, StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalCommandBar_IsReducedToProjectSaveOverflowAndWordExport()
    {
        var root = SolutionRootLocator.Find();
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");

        Assert.Contains("Content=\"Word Dosyası Oluştur\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Kaydet\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Header=\"Farklı Kaydet\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProjectCommand", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProjectAsCommand", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"＋ Yeni\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Aç\"", shell, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
