namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint23RangeIntelligenceArchitectureTests
{
    [Fact]
    public void SourcePreview_NoLongerHasAHardCodedOneHundredRowDefault()
    {
        var root = SolutionRootLocator.Find();
        var contract = Read(root, "src", "KKL.WordStudio.Application", "Excel", "IExcelWorkbookReader.cs");
        var reader = Read(root, "src", "KKL.WordStudio.Infrastructure", "Excel", "OpenXmlExcelWorkbookReader.cs");

        Assert.Contains("maxPreviewRows = int.MaxValue", contract, StringComparison.Ordinal);
        Assert.Contains("maxPreviewRows = int.MaxValue", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("maxPreviewRows = 100", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("maxPreviewRows = 100", reader, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticHeaderDetection_ExtendsTheExistingDetectorInsteadOfAddingAnotherExcelReader()
    {
        var root = SolutionRootLocator.Find();
        var detector = Read(root, "src", "KKL.WordStudio.Application", "Excel", "ExcelDataRangeDetector.cs");
        var infrastructureRoot = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure");
        var readers = Directory
            .EnumerateFiles(infrastructureRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(": IExcelWorkbookReader", StringComparison.Ordinal))
            .ToList();

        Assert.Contains("ExcelSemanticFieldMatcher", detector, StringComparison.Ordinal);
        Assert.Contains("SemanticFields", detector, StringComparison.Ordinal);
        Assert.Single(readers);
        Assert.EndsWith("OpenXmlExcelWorkbookReader.cs", readers[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ManualRangePersistence_DoesNotForceAWorkingDataSnapshot()
    {
        var root = SolutionRootLocator.Find();
        var persistence = Read(
            root,
            "src",
            "KKL.WordStudio.Application",
            "WorkingData",
            "WorksheetRangePersistenceExtensions.cs");
        var viewModel = Read(
            root,
            "src",
            "KKL.WordStudio.UI",
            "ViewModels",
            "ExcelWorkspaceViewModel.RangePersistence.cs");

        Assert.Contains("SaveSelectedRange", persistence, StringComparison.Ordinal);
        Assert.Contains("worksheet.SelectedRange = Clone(range);", persistence, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadWorkingDataAsync", persistence, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkingData =", persistence, StringComparison.Ordinal);
        Assert.Contains("OnIsRangeEditorOpenChanged", viewModel, StringComparison.Ordinal);
        Assert.Contains("RangeIsAutomaticCandidate", viewModel, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
