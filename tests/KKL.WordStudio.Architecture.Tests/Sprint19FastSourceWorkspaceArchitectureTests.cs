namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint19FastSourceWorkspaceArchitectureTests
{
    [Fact]
    public void ProjectExplorerShellAndTypes_AreRemoved()
    {
        var root = SolutionRootLocator.Find();
        var mainXaml = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var mainCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");
        var mainViewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "MainViewModel.cs");
        var app = Read(root, "src", "KKL.WordStudio.UI", "App.xaml.cs");

        Assert.DoesNotContain("Proje Gezgini", mainXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectExplorerHost", mainXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsProjectExplorerOpen", mainXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectExplorerView", mainCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectExplorer", mainViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectExplorer", app, StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(root, "src", "KKL.WordStudio.UI", "Views", "ProjectExplorerView.xaml")));
        Assert.False(File.Exists(Path.Combine(root, "src", "KKL.WordStudio.UI", "Views", "ProjectExplorerView.xaml.cs")));
        Assert.False(File.Exists(Path.Combine(root, "src", "KKL.WordStudio.UI", "ViewModels", "ProjectExplorerViewModel.cs")));
        Assert.False(File.Exists(Path.Combine(root, "src", "KKL.WordStudio.UI", "ViewModels", "ProjectExplorerNodeViewModel.cs")));
    }

    [Fact]
    public void LoadedSourcesSelector_UsesTheExistingExcelWorkspaceState()
    {
        var root = SolutionRootLocator.Find();
        var mainXaml = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var mainCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");
        var sourceSelector = Read(root, "src", "KKL.WordStudio.UI", "Views", "LoadedSourcesView.xaml");
        var app = Read(root, "src", "KKL.WordStudio.UI", "App.xaml.cs");

        Assert.Contains("LoadedSourcesHost", mainXaml, StringComparison.Ordinal);
        Assert.Contains("LoadedSourcesHost.Content = loadedSourcesView", mainCode, StringComparison.Ordinal);
        Assert.Contains("Yüklenen Kaynaklar", sourceSelector, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding OpenWorkbooks}\"", sourceSelector, StringComparison.Ordinal);
        Assert.Contains("SelectedItem=\"{Binding SelectedWorkbook, Mode=TwoWay}\"", sourceSelector, StringComparison.Ordinal);
        Assert.Contains("SheetNames.Count", sourceSelector, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenExcelFileCommand}\"", sourceSelector, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<LoadedSourcesView>()", app, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkingDataGrid_RestoresStableCurrentCellAndKeyboardFocusAfterMutations()
    {
        var root = SolutionRootLocator.Find();
        var source = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.KeyboardFlow.cs");

        Assert.Contains("GridKeyboardAnchor", source, StringComparison.Ordinal);
        Assert.Contains("GetColumnIdentity(current.Column)", source, StringComparison.Ordinal);
        Assert.Contains("nameof(ExcelWorkspaceViewModel.PreviewTable)", source, StringComparison.Ordinal);
        Assert.Contains("WorkingDataGrid.CurrentCell = cell", source, StringComparison.Ordinal);
        Assert.Contains("WorkingDataGrid.SelectedCells.Add(cell)", source, StringComparison.Ordinal);
        Assert.Contains("WorkingDataGrid.ScrollIntoView(item, column)", source, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Input", source, StringComparison.Ordinal);
        Assert.Contains("Keyboard.Focus(WorkingDataGrid)", source, StringComparison.Ordinal);
        Assert.Contains("case \"Kopyala\"", source, StringComparison.Ordinal);
        Assert.Contains("case \"Yapıştır\"", source, StringComparison.Ordinal);
        Assert.Contains("e.Key is Key.V or Key.Z or Key.Y", source, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
