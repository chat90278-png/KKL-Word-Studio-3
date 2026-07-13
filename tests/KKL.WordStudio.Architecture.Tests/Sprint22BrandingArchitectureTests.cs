namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22BrandingArchitectureTests
{
    [Fact]
    public void Branding_UsesSelectedSmallSizeMarkWithoutRuntimeIcoConversion()
    {
        var root = SolutionRootLocator.Find();
        var project = Read(root, "src", "KKL.WordStudio.UI", "KKL.WordStudio.UI.csproj");
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var shellCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");

        Assert.Contains("<ApplicationIcon>Assets\\Brand\\AppIcon.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Assets\\Brand\\BrandMark.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Assets\\Brand\\BrandMarkSmall.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("Source=\"Assets/Brand/BrandMarkSmall.png\"", shell, StringComparison.Ordinal);
        Assert.Contains("BrandMarkSmall.png", shellCode, StringComparison.Ordinal);
        Assert.Contains("BitmapFrame.Create", shellCode, StringComparison.Ordinal);
        Assert.Contains("Branding must never block application startup", shellCode, StringComparison.Ordinal);

        // Loading the ICO again through Window.Icon invokes WPF's runtime
        // TypeConverter and previously failed before MainWindow was shown.
        Assert.DoesNotContain("Icon=\"Assets/Brand/AppIcon.ico\"", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationIcon_HasAValidWindowsIconDirectoryHeader()
    {
        var root = SolutionRootLocator.Find();
        var icon = File.ReadAllBytes(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.UI",
            "Assets",
            "Brand",
            "AppIcon.ico"));

        Assert.True(icon.Length >= 6, "AppIcon.ico is too short to contain an ICONDIR header.");
        Assert.Equal(new byte[] { 0, 0, 1, 0 }, icon[..4]);
        Assert.InRange(BitConverter.ToUInt16(icon, 4), (ushort)1, (ushort)256);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
