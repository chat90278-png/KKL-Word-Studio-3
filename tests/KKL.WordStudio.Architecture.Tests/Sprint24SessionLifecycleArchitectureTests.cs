namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint24SessionLifecycleArchitectureTests
{
    [Fact]
    public void ShellViewModel_HasOneInMemorySessionAndNoNativeProjectCommands()
    {
        var root = SolutionRootLocator.Find();
        var source = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "MainViewModel.cs");
        var factory = Read(root, "src", "KKL.WordStudio.Application", "Workspace", "WorkspaceSessionFactory.cs");

        Assert.Contains("WorkspaceSessionFactory.CreateDefault()", source, StringComparison.Ordinal);
        Assert.Contains("_workspace.SetActiveProject", source, StringComparison.Ordinal);
        Assert.Contains("ExportToWordAsync", source, StringComparison.Ordinal);
        Assert.Contains("new Project", factory, StringComparison.Ordinal);
        Assert.Contains("new Report", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("IProjectService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NewProject()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenProjectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProjectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProjectAsAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentProjectFilePath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FileDialogContract_ContainsOnlyExcelWordUserFlow()
    {
        var root = SolutionRootLocator.Find();
        var contract = Read(root, "src", "KKL.WordStudio.UI", "Services", "IFileDialogService.cs");
        var implementation = Read(root, "src", "KKL.WordStudio.UI", "Services", "FileDialogService.cs");

        Assert.Contains("OpenExcelFile", contract, StringComparison.Ordinal);
        Assert.Contains("OpenWordDocument", contract, StringComparison.Ordinal);
        Assert.Contains("SaveWordFile", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenProjectFile", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProjectFile", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("*.kws", implementation, StringComparison.Ordinal);
        Assert.DoesNotContain("Proje Aç", implementation, StringComparison.Ordinal);
        Assert.DoesNotContain("Projeyi Kaydet", implementation, StringComparison.Ordinal);
    }

    [Fact]
    public void TitleBar_DoesNotExposeInternalProjectAggregateName()
    {
        var root = SolutionRootLocator.Find();
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");

        Assert.Contains("Text=\"KKL Word Studio\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentProject.Name", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveProjectCommand", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenProjectCommand", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickReport_ReusesExistingTransferServiceWithoutProjectLifecycleFallback()
    {
        var root = SolutionRootLocator.Find();
        var quick = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ExcelWorkspaceViewModel.QuickAssembly.cs");
        var normal = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ExcelWorkspaceViewModel.TransferPlacement.cs");

        Assert.Contains("ExcelTransferPlacementCoordinator.Transfer", quick, StringComparison.Ordinal);
        Assert.Contains("_transferService", quick, StringComparison.Ordinal);
        Assert.Contains("ExcelTransferPlacementCoordinator.Transfer", normal, StringComparison.Ordinal);
        Assert.DoesNotContain("new ExcelReportTransferService", quick, StringComparison.Ordinal);
        Assert.DoesNotContain("önce bir proje oluşturun veya açın", quick, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rapor çalışma alanı hazır değil.", quick, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeProjectPersistence_IsAbsentFromProductionComposition()
    {
        var root = SolutionRootLocator.Find();
        var productionFiles = SourceScan.ReadCodeFiles(root, "src", ".cs");
        var forbiddenTokens = new[]
        {
            "IProjectService",
            "KwsProjectRepository",
            "KwsProjectManifest",
            "ProjectFileExtension",
            "ProjectFileFormatVersion",
            "\".kws\""
        };

        foreach (var token in forbiddenTokens)
        {
            var offenders = productionFiles
                .Where(file => file.Text.Contains(token, StringComparison.Ordinal))
                .Select(file => file.RelativePath)
                .ToList();
            Assert.True(
                offenders.Count == 0,
                $"Native project lifecycle token '{token}' remains in production: {string.Join(", ", offenders)}");
        }

        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.Application",
            "Abstractions",
            "IProjectService.cs")));
        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.Infrastructure",
            "Persistence",
            "KwsProjectRepository.cs")));
        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.Infrastructure",
            "Persistence",
            "KwsProjectManifest.cs")));
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
