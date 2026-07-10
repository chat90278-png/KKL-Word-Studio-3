namespace KKL.WordStudio.Architecture.Tests;

using Xunit;
using Xunit.Abstractions;

public sealed class BaselineRegressionInventoryTests
{
    private readonly ITestOutputHelper _output;

    public BaselineRegressionInventoryTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Sprint14ContractBaselineTests_RemainPresentAndInventoryIsReported()
    {
        var root = SolutionRootLocator.Find();
        var comparison = TestInventory.CompareToBaseline(root);

        _output.WriteLine($"Total integrated test methods: {comparison.TotalTestMethods}");
        _output.WriteLine($"Current skipped tests: {comparison.SkippedTests.Count}");
        foreach (var skipped in comparison.SkippedTests)
            _output.WriteLine($"  SKIP {skipped.RelativePath}::{skipped.MethodName}");
        _output.WriteLine($"Sprint 15 baseline manifest methods: {comparison.BaselineManifestCount}");
        _output.WriteLine($"Sprint 15 baseline manifest files: {comparison.BaselineManifestFileCount}");
        _output.WriteLine($"Sprint 15 baseline skipped tests: {comparison.BaselineSkippedTestCount}");
        _output.WriteLine($"Removed baseline test files: {comparison.RemovedTestFiles.Count}");
        foreach (var removedFile in comparison.RemovedTestFiles)
            _output.WriteLine($"  REMOVED FILE {removedFile}");
        _output.WriteLine($"Removed baseline test methods: {comparison.RemovedTestMethods.Count}");
        foreach (var removedMethod in comparison.RemovedTestMethods)
            _output.WriteLine($"  REMOVED METHOD {removedMethod}");

        Assert.True(
            comparison.RemovedTestFiles.Count == 0,
            $"Sprint 15 baseline test files were removed: {string.Join(", ", comparison.RemovedTestFiles)}");
        Assert.True(
            comparison.RemovedTestMethods.Count == 0,
            $"Sprint 15 baseline test methods were removed/renamed: {string.Join(", ", comparison.RemovedTestMethods)}");
        Assert.True(
            comparison.TotalTestMethods >= comparison.BaselineManifestCount,
            $"Current test method count {comparison.TotalTestMethods} is below Sprint 15 contract baseline {comparison.BaselineManifestCount}.");
    }
}
