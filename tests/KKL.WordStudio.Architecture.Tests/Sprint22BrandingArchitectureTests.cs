namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22BrandingArchitectureTests
{
    [Fact]
    public void Branding_UsesGeneratedCompileTimeIconWithoutRuntimeIcoConversion()
    {
        var root = SolutionRootLocator.Find();
        var project = Read(root, "src", "KKL.WordStudio.UI", "KKL.WordStudio.UI.csproj");
        var shell = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml");
        var shellCode = Read(root, "src", "KKL.WordStudio.UI", "MainWindow.xaml.cs");
        var generator = Read(root, "scripts", "generate-app-icon.ps1");

        Assert.Contains("<GeneratedApplicationIcon>$(MSBuildProjectDirectory)\\$(IntermediateOutputPath)Generated\\AppIcon.ico</GeneratedApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("<ApplicationIcon>$(GeneratedApplicationIcon)</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("<Target Name=\"GenerateApplicationIcon\"", project, StringComparison.Ordinal);
        Assert.Contains("BeforeTargets=\"CoreCompile\"", project, StringComparison.Ordinal);
        Assert.Contains("Assets\\Brand\\AppIcon.base64", project, StringComparison.Ordinal);
        Assert.Contains("scripts\\generate-app-icon.ps1", project, StringComparison.Ordinal);
        Assert.DoesNotContain("<Resource Include=\"Assets\\Brand\\AppIcon.ico\" />", project, StringComparison.Ordinal);

        Assert.Contains("<Resource Include=\"Assets\\Brand\\BrandMark.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Assets\\Brand\\BrandMarkSmall.png\" />", project, StringComparison.Ordinal);
        Assert.Contains("Source=\"Assets/Brand/BrandMarkSmall.png\"", shell, StringComparison.Ordinal);
        Assert.Contains("BrandMarkSmall.png", shellCode, StringComparison.Ordinal);
        Assert.Contains("BitmapFrame.Create", shellCode, StringComparison.Ordinal);
        Assert.Contains("Branding must never block application startup", shellCode, StringComparison.Ordinal);

        Assert.Contains("[Convert]::FromBase64String", generator, StringComparison.Ordinal);
        Assert.Contains("[IO.File]::WriteAllBytes", generator, StringComparison.Ordinal);

        // Loading an ICO through Window.Icon invokes WPF's runtime TypeConverter.
        // The application icon is generated only for the native executable at compile time.
        Assert.DoesNotContain("Icon=\"Assets/Brand/AppIcon.ico\"", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationIconSource_HasAValidWindowsIconDirectoryHeader()
    {
        var root = SolutionRootLocator.Find();
        var encoded = Read(
            root,
            "src",
            "KKL.WordStudio.UI",
            "Assets",
            "Brand",
            "AppIcon.base64").Trim();
        var icon = Convert.FromBase64String(encoded);

        Assert.True(icon.Length >= 6, "AppIcon.base64 is too short to contain an ICONDIR header.");
        Assert.Equal(new byte[] { 0, 0, 1, 0 }, icon[..4]);
        Assert.InRange(BitConverter.ToUInt16(icon, 4), (ushort)1, (ushort)256);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
