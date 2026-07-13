namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22BrandingArchitectureTests
{
    [Fact]
    public void Branding_UsesBuildTimeApplicationIconAndSafePngWindowMark()
    {
        var root = SolutionRootLocator.Find();
        var project = Read(root, "src", "KKL.WordStudio.UI", "KKL.WordStudio.UI.csproj");
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");

        Assert.Contains("<ApplicationIcon>Assets\\Brand\\AppIcon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Assets\\Brand\\BrandMark.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("Source=\"Assets/Brand/BrandMark.png\"", shell, StringComparison.Ordinal);

        // The executable icon is inherited from ApplicationIcon. Loading the ICO
        // again through Window.Icon invokes WPF's runtime TypeConverter and can
        // fail before MainWindow is shown.
        Assert.DoesNotContain("Icon=\"Assets/Brand/AppIcon.ico\"", shell, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
