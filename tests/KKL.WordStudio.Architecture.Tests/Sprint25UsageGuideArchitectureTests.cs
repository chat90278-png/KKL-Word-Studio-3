namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint25UsageGuideArchitectureTests
{
    [Fact]
    public void Shell_ExposesUsageGuideAsAnInApplicationPage()
    {
        var root = SolutionRootLocator.Find();
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var shellCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");
        var mainViewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "MainViewModel.cs");
        Assert.Contains("Kullanım Kılavuzu", shell, StringComparison.Ordinal);
        Assert.Contains("ShowUsageGuideCommand", shell, StringComparison.Ordinal);
        Assert.Contains("UsageGuideHost", shell, StringComparison.Ordinal);
        Assert.Contains("IsUsageGuideOpen", shell, StringComparison.Ordinal);
        Assert.Contains("UsageGuideView usageGuideView", shellCode, StringComparison.Ordinal);
        Assert.Contains("UsageGuideHost.Content = usageGuideView", shellCode, StringComparison.Ordinal);
        Assert.Contains("CloseUsageGuide", mainViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowDialog", shellCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Guide_UsesApprovedFruitDemoWorkflowAndRealEmbeddedScreens()
    {
        var root = SolutionRootLocator.Find();
        var project = Read(root, "src", "KKL.WordStudio.UI", "KKL.WordStudio.UI.csproj");
        var guide = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "UsageGuideViewModel.cs");
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "UsageGuideView.xaml");
        var assetDirectory = Path.Combine(root, "src", "KKL.WordStudio.UI", "Assets", "GuideScreens");
        Assert.Contains("Assets\\GuideScreens\\*.base64", project, StringComparison.Ordinal);
        Assert.Contains("Parca_Listesi", guide, StringComparison.Ordinal);
        Assert.Contains("No, Tr İsim, Parça Numarası, NSN, Seri Numarası ve Adet", guide, StringComparison.Ordinal);
        Assert.Contains("Ne işe yarar?", view, StringComparison.Ordinal);
        Assert.Contains("Adım adım kullanım", view, StringComparison.Ordinal);
        Assert.Contains("İpucu / Dikkat", view, StringComparison.Ordinal);

        var expectedScreens = new[] { "01-ana-ekran-bos.jpg.base64", "02-demo-excel-yuklu.jpg.base64", "16-veri-araligi-birlesik.jpg.base64", "03-worde-aktar-yerlesim-onayi.jpg.base64", "04-preview-ve-icindekiler.jpg.base64", "05-tablo-ozellikleri.jpg.base64", "06-baslik-ozellikleri.jpg.base64", "07-uyarilar.jpg.base64", "17-hizli-rapor-birlesik.jpg.base64", "11-word-ciktisi.jpg.base64" };
        foreach (var screen in expectedScreens)
        {
            var path = Path.Combine(assetDirectory, screen);
            Assert.True(File.Exists(path), $"Missing guide screen: {screen}");
            var encoded = string.Concat(File.ReadAllText(path).Where(c => !char.IsWhiteSpace(c)));
            Assert.True(encoded.Length > 100, $"Guide screen is empty: {screen}");
            Assert.StartsWith("/9j/", encoded, StringComparison.Ordinal);
            Assert.All(encoded, c => Assert.True(char.IsLetterOrDigit(c) || c is '+' or '/' or '=', $"Invalid Base64 character in {screen}"));
        }
    }

    [Fact]
    public void Guide_RemainsUiOnlyAndDoesNotOwnWorkspaceOrExportServices()
    {
        var root = SolutionRootLocator.Find();
        var guide = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "UsageGuideViewModel.cs");
        var section = Read(root, "src", "KKL.WordStudio.UI", "Models", "UsageGuideSection.cs");
        var loader = Read(root, "src", "KKL.WordStudio.UI", "Services", "GuideImageSourceLoader.cs");
        Assert.DoesNotContain("IWorkspace", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("IReportExporter", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("IExcelWorkbookReader", guide, StringComparison.Ordinal);
        Assert.Contains("UI-only", section, StringComparison.Ordinal);
        Assert.Contains("embedded Base64 resources", loader, StringComparison.Ordinal);
    }

    [Fact]
    public void Guide_ProvidesLocalEditableContentWithoutMutatingWorkspace()
    {
        var root = SolutionRootLocator.Find();
        var guide = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "UsageGuideViewModel.cs");
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "UsageGuideView.xaml");
        Assert.Contains("Düzenleme Modu", view, StringComparison.Ordinal);
        Assert.Contains("SaveEditsCommand", view, StringComparison.Ordinal);
        Assert.Contains("Görseli Değiştir", view, StringComparison.Ordinal);
        Assert.Contains("LocalApplicationData", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("IWorkspace", guide, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) => File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}