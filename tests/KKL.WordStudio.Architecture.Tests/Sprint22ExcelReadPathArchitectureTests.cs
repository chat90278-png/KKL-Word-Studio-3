namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22ExcelReadPathArchitectureTests
{
    [Fact]
    public void WorkingDataRead_RemainsSingleReaderAndAvoidsFullSheetPreScans()
    {
        var root = SolutionRootLocator.Find();
        var reader = File.ReadAllText(Path.Combine(
            root,
            "src",
            "KKL.WordStudio.Infrastructure",
            "Excel",
            "OpenXmlExcelWorkbookReader.cs"));

        Assert.Contains("Task.Run(", reader, StringComparison.Ordinal);
        Assert.Contains("firstRelevantRow", reader, StringComparison.Ordinal);
        Assert.Contains("bufferedRows", reader, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException)", reader, StringComparison.Ordinal);

        Assert.DoesNotContain("ResolveMaxColumn", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("rowLookup", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("ToDictionary(row => (int)row.RowIndex", reader, StringComparison.Ordinal);
    }

    [Fact]
    public void ExcelReadHardening_DoesNotIntroduceAnotherWorkbookReader()
    {
        var root = SolutionRootLocator.Find();
        var infrastructureRoot = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure");
        var implementations = Directory
            .EnumerateFiles(infrastructureRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(": IExcelWorkbookReader", StringComparison.Ordinal))
            .ToList();

        Assert.Single(implementations);
        Assert.EndsWith("OpenXmlExcelWorkbookReader.cs", implementations[0], StringComparison.Ordinal);
    }
}
