namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint23CurrentContractCharacterizationTests
{
    [Fact]
    public void ExcelWorkspace_CurrentlyHostsMappingDrawerRangeEditorAndLegacyTransferChoices()
    {
        var root = SolutionRootLocator.Find();
        var xaml = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.xaml");

        Assert.Contains("Content=\"Sütunları Eşle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("SÜTUNLARI SEÇ VE EŞLE", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Veri Aralığını Düzenle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Veriyi Yeniden Bağla (sütunları koru)", xaml, StringComparison.Ordinal);
        Assert.Contains("Sütunları Kaynaktan Yenile ve Bağla", xaml, StringComparison.Ordinal);
        Assert.Contains("Kaynak Olarak Ekle", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkingDataGrid_CurrentlyReplacesExcelLettersWithWorkingHeadersAndDoesNotDisableSorting()
    {
        var root = SolutionRootLocator.Find();
        var xaml = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.xaml");
        var codeBehind = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.xaml.cs");

        Assert.Contains("AutoGenerateColumns=\"True\"", xaml, StringComparison.Ordinal);
        Assert.Contains("var workingHeader = _viewModel.GetWorkingDataColumnHeader(e.PropertyName);", codeBehind, StringComparison.Ordinal);
        Assert.Contains("e.Column.Header = workingHeader;", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("CanUserSortColumns=\"False\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_CurrentlyUsesFixedExcelPreviewDockColumnsAndShowsNewOpenCommands()
    {
        var root = SolutionRootLocator.Find();
        var xaml = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");

        Assert.Contains("Content=\"＋ Yeni\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Aç\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ContentControl x:Name=\"ExcelWorkspaceHost\" Grid.Column=\"0\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<ContentControl x:Name=\"PreviewHost\" Grid.Column=\"2\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<ContentControl x:Name=\"ContextDockHost\" Grid.Column=\"4\" />", xaml, StringComparison.Ordinal);
        Assert.Contains("<ColumnDefinition x:Name=\"DockColumn\" Width=\"350\" />", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentsAndWarnings_CurrentlyProjectFlatReportOrderAndUseSingleLevelPreviewDiagnostics()
    {
        var root = SolutionRootLocator.Find();
        var contents = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ContentsViewModel.cs");
        var structure = Read(root, "src", "KKL.WordStudio.Application", "Structure", "ReportStructureService.cs");
        var warnings = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "WarningCenterViewModel.cs");

        Assert.Contains("a flat sequence of Heading/AltHeading/Table", contents, StringComparison.Ordinal);
        Assert.Contains("section.Root.Children", contents, StringComparison.Ordinal);
        Assert.Contains("never introduces a second outline", structure, StringComparison.Ordinal);
        Assert.Contains("public int Count => Items.Count;", warnings, StringComparison.Ordinal);
        Assert.Contains("public string HeaderText => HasItems ? $\"{Count} uyarı bulundu\" : \"Uyarı yok\";", warnings, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticSeverity", warnings, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
