namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint20AuthoringSurfaceSimplificationTests
{
    [Fact]
    public void ExcelSourceImport_HasOneVisibleAddAction()
    {
        var root = SolutionRootLocator.Find();
        var loadedSources = Read(root, "src", "KKL.WordStudio.UI", "Views", "LoadedSourcesView.xaml");
        var excelWorkspace = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.xaml");

        Assert.Contains("Text=\"Excel ekle\"", loadedSources, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Excel Dosyası Aç", excelWorkspace, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"veya Excel Dosyası Aç", excelWorkspace, StringComparison.Ordinal);
    }

    [Fact]
    public void TableCaptionProperties_UseDirectTextEntryOnly()
    {
        var root = SolutionRootLocator.Find();
        var properties = Read(root, "src", "KKL.WordStudio.UI", "Views", "PropertiesView.xaml");

        Assert.Contains("Text=\"Tablo Başlığı\"", properties, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding TableCaption", properties, StringComparison.Ordinal);
        Assert.DoesNotContain("Başlıktan Al", properties, StringComparison.Ordinal);
        Assert.DoesNotContain("HeadingCaptionCandidates", properties, StringComparison.Ordinal);
        Assert.DoesNotContain("UseSelectedHeadingAsTableCaptionCommand", properties, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
