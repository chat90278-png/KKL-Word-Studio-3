namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22BrandingArchitectureTests
{
    [Fact]
    public void Branding_UsesBuildTimeFallbackAndSafePngRuntimeIcon()
    {
        var root = SolutionRootLocator.Find();
        var project = Read(root, "src", "KKL.WordStudio.UI", "KKL.WordStudio.UI.csproj");
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var shellCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");

        Assert.Contains("<ApplicationIcon>Assets\\Brand\\AppIcon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Assets\\Brand\\BrandMark.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Assets\\Brand\\BrandMarkSmall.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("Source=\"Assets/Brand/BrandMark.png\"", shell, StringComparison.Ordinal);
        Assert.Contains("BrandMarkSmall.png", shellCode, StringComparison.Ordinal);
        Assert.Contains("BitmapFrame.Create", shellCode, StringComparison.Ordinal);
        Assert.Contains("Branding must never block application startup", shellCode, StringComparison.Ordinal);

        // Loading the ICO again through Window.Icon invokes WPF's runtime
        // TypeConverter and previously failed before MainWindow was shown.
        Assert.DoesNotContain("Icon=\"Assets/Brand/AppIcon.ico\"", shell, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
