namespace KKL.WordStudio.Architecture.Tests;

using Xunit;
using System.Xml.Linq;

public sealed class EngineProjectDependencyTests
{
    [Fact]
    public void EngineProject_DoesNotReferenceUIInfrastructureOrRendering()
    {
        var root = SolutionRootLocator.Find();
        var projectPath = Path.Combine(root, "src", "KKL.WordStudio.Engine", "KKL.WordStudio.Engine.csproj");
        var project = XDocument.Load(projectPath);

        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(projectReferences, reference => reference.Contains("KKL.WordStudio.UI", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, reference => reference.Contains("KKL.WordStudio.Infrastructure", StringComparison.Ordinal));
        Assert.DoesNotContain(projectReferences, reference => reference.Contains("KKL.WordStudio.Rendering", StringComparison.Ordinal));

        var packageReferences = project
            .Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include") ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(packageReferences, package => package.Contains("OpenXml", StringComparison.OrdinalIgnoreCase));
    }
}
