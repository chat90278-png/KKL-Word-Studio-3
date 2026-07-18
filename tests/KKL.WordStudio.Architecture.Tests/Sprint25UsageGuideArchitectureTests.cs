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
    public void Guide_UsesGeneralProductWorkflowAndEmbeddedJpegScreens()
    {
        var root = SolutionRootLocator.Find();
        var project = Read(root, "src", "KKL.WordStudio.UI", "KKL.WordStudio.UI.csproj");
        var guide = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "UsageGuideViewModel.cs");
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "UsageGuideView.xaml");
        var assets = Path.Combine(root, "src", "KKL.WordStudio.UI", "Assets", "GuideScreens");

        Assert.Contains("Assets\\GuideScreens\\*.jpg", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Assets\\GuideScreens\\*.base64", project, StringComparison.Ordinal);
        Assert.Contains("Excel Kaynağı ve Sayfa Seçimi", guide, StringComparison.Ordinal);
        Assert.Contains("Sütun Ekleme ve Silme", guide, StringComparison.Ordinal);
        Assert.Contains("Sola Sütun Ekle", guide, StringComparison.Ordinal);
        Assert.Contains("Sağa Sütun Ekle", guide, StringComparison.Ordinal);
        Assert.Contains("Sütunu Sil", guide, StringComparison.Ordinal);
        Assert.Contains("Sütunu Gizle", guide, StringComparison.Ordinal);
        Assert.Contains("Ekran görüntüsündeki örnek kayıtlar yalnızca arayüzü göstermek içindir", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("No, Tr İsim, Parça Numarası, NSN, Seri Numarası ve Adet", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("Meyveli demo veri üzerinden", view, StringComparison.Ordinal);
        Assert.Contains("Ekran görüntüsündeki örnek veriler yalnızca arayüzü göstermek için kullanılmıştır", view, StringComparison.Ordinal);
        Assert.Contains("StretchDirection=\"DownOnly\"", view, StringComparison.Ordinal);
        Assert.Contains("BitmapScalingMode=\"HighQuality\"", view, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(assets, "*.base64", SearchOption.TopDirectoryOnly));

        foreach (var name in ScreenNames)
        {
            var path = Path.Combine(assets, name);
            Assert.True(File.Exists(path), $"Missing guide screen: {name}");
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1_000, $"Guide screen is empty: {name}");
            Assert.True(IsJpeg(bytes), $"Guide screen is not JPEG: {name}");
        }
    }

    [Fact]
    public void Guide_RemainsUiOnly()
    {
        var root = SolutionRootLocator.Find();
        var guide = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "UsageGuideViewModel.cs");
        var store = Read(root, "src", "KKL.WordStudio.UI", "Services", "UsageGuideContentStore.cs");

        foreach (var forbidden in new[] { "IWorkspace", "IReportExporter", "IExcelWorkbookReader" })
        {
            Assert.DoesNotContain(forbidden, guide, StringComparison.Ordinal);
            Assert.DoesNotContain(forbidden, store, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Guide_ProvidesPersistentTextAndImageEditing()
    {
        var root = SolutionRootLocator.Find();
        var guide = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "UsageGuideViewModel.cs");
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "UsageGuideView.xaml");
        var store = Read(root, "src", "KKL.WordStudio.UI", "Services", "UsageGuideContentStore.cs");

        foreach (var text in new[] { "Düzenleme Modu", "Görseli Değiştir", "Varsayılan Görsel", "Seçili Bölümü Varsayılana Döndür" })
            Assert.Contains(text, view, StringComparison.Ordinal);

        foreach (var command in new[] { "BeginEditCommand", "SaveEditsCommand", "CancelEditsCommand", "ResetSectionCommand" })
            Assert.Contains(command, view, StringComparison.Ordinal);

        Assert.Contains("UsageGuideContentStore", guide, StringComparison.Ordinal);
        Assert.Contains("LocalApplicationData", store, StringComparison.Ordinal);
        Assert.Contains("usage-guide.json", store, StringComparison.Ordinal);
        Assert.Contains("OpenFileDialog", store, StringComparison.Ordinal);
    }

    private static readonly string[] ScreenNames =
    [
        "01-ana-ekran-bos.jpg", "02-demo-excel-yuklu.jpg",
        "16-veri-araligi-birlesik.jpg", "03-worde-aktar-yerlesim-onayi.jpg",
        "04-preview-ve-icindekiler.jpg", "05-tablo-ozellikleri.jpg",
        "06-baslik-ozellikleri.jpg", "07-uyarilar.jpg",
        "17-hizli-rapor-birlesik.jpg", "11-word-ciktisi.jpg"
    ];

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
