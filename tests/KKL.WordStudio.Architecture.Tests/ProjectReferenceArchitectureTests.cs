namespace KKL.WordStudio.Architecture.Tests;

using System.Xml.Linq;
using Xunit;

public sealed class ProjectReferenceArchitectureTests
{
    [Fact]
    public void ProductionProjectReferences_RespectSprint14LayerBoundaries()
    {
        var root = SolutionRootLocator.Find();

        AssertReferencesAreSubset(root, "Domain", "Shared");
        AssertReferencesExclude(root, "Application", "Engine", "Infrastructure", "Rendering", "UI");
        AssertReferencesAreSubset(root, "Engine", "Application", "Shared");
        AssertReferencesExclude(root, "Infrastructure", "UI");
        AssertReferencesAreSubset(root, "Rendering", "Domain", "Shared");
        AssertReferencesAreSubset(root, "Shared");

        // UI is the composition root and may reference all current layers.
        _ = LoadProject(root, "UI");
    }

    [Fact]
    public void ArchitectureTests_AreNotReferencedByProductionProjects()
    {
        var root = SolutionRootLocator.Find();
        var offenders = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => GetProjectReferences(path).Any(reference =>
                reference.Contains("KKL.WordStudio.Architecture.Tests", StringComparison.OrdinalIgnoreCase)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"Architecture.Tests must never become a production dependency. Offending projects: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void EngineProject_DoesNotReferenceOpenXmlPackage()
    {
        var root = SolutionRootLocator.Find();
        var projectPath = LoadProject(root, "Engine");
        var packages = XDocument.Load(projectPath)
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(packages, package => package.Contains("OpenXml", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertReferencesAreSubset(string root, string layer, params string[] allowedLayers)
    {
        var projectPath = LoadProject(root, layer);
        var allowed = allowedLayers
            .Select(ProjectName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = GetProjectReferences(projectPath).ToList();
        var forbidden = references.Where(reference => !allowed.Contains(reference)).ToList();

        Assert.True(
            forbidden.Count == 0,
            $"{layer} may reference only [{string.Join(", ", allowedLayers)}]. " +
            $"Forbidden project reference(s): {string.Join(", ", forbidden)}. Project: {Path.GetRelativePath(root, projectPath)}");
    }

    private static void AssertReferencesExclude(string root, string layer, params string[] forbiddenLayers)
    {
        var projectPath = LoadProject(root, layer);
        var forbidden = forbiddenLayers
            .Select(ProjectName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var references = GetProjectReferences(projectPath).ToList();
        var offenders = references.Where(forbidden.Contains).ToList();

        Assert.True(
            offenders.Count == 0,
            $"{layer} must not reference [{string.Join(", ", forbiddenLayers)}]. " +
            $"Offending project reference(s): {string.Join(", ", offenders)}. Project: {Path.GetRelativePath(root, projectPath)}");
    }

    private static string LoadProject(string root, string layer)
    {
        var path = Path.Combine(root, "src", ProjectName(layer), $"{ProjectName(layer)}.csproj");
        Assert.True(File.Exists(path), $"Expected production project was not found: {path}");
        return path;
    }

    private static IEnumerable<string> GetProjectReferences(string projectPath) =>
        XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', Path.DirectorySeparatorChar)))
            .Where(name => !string.IsNullOrWhiteSpace(name));

    private static string ProjectName(string layer) => $"KKL.WordStudio.{layer}";
}
