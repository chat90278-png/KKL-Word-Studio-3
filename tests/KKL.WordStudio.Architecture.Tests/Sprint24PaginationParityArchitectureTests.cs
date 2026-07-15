namespace KKL.WordStudio.Architecture.Tests;

using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint24PaginationParityArchitectureTests
{
    [Fact]
    public void PreviewAndWord_ConsumeOneSemanticPaginationPolicy()
    {
        var root = SolutionRootLocator.Find();
        var policy = Read(root, "src", "KKL.WordStudio.Application", "Content", "ReportFlowPaginationPolicy.cs");
        var previewFlow = Read(root, "src", "KKL.WordStudio.Engine", "Layout", "LayoutPageFlow.cs");
        var previewTable = Read(root, "src", "KKL.WordStudio.Engine", "Layout", "DeterministicTablePaginator.cs");
        var word = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordContentWriter.cs");
        var exporter = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "WordExporter.cs");

        Assert.Contains("ResolveKeepWithNextChainEndIndex", policy, StringComparison.Ordinal);
        Assert.Contains("ResolveMinimumTableStartDataRowCount", policy, StringComparison.Ordinal);
        Assert.Contains("KeepTableCaptionWithTable", policy, StringComparison.Ordinal);
        Assert.Contains("KeepTableRowsIntact", policy, StringComparison.Ordinal);
        Assert.Contains("RemoveTrailingHeadingChainWhenPageHasEarlierBodyContent", previewFlow, StringComparison.Ordinal);
        Assert.Contains("ReportFlowPaginationPolicy.IsHeading", previewFlow, StringComparison.Ordinal);
        Assert.Contains("ResolveMinimumTableStartDataRowCount", previewTable, StringComparison.Ordinal);
        Assert.Contains("EstimateRequiredStartRowsHeight", previewTable, StringComparison.Ordinal);
        Assert.Contains("ResolveMinimumTableStartDataRowCount", word, StringComparison.Ordinal);
        Assert.Contains("new KeepNext()", word, StringComparison.Ordinal);
        Assert.Contains("new CantSplit()", word, StringComparison.Ordinal);
        Assert.Contains("WordTableWriter.BuildTable(table)", word, StringComparison.Ordinal);
        Assert.Contains("ReportFlowPaginationPolicy.StartsNewPageAfterTable", exporter, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildPageBreakParagraph", word, StringComparison.Ordinal);
    }

    [Fact]
    public void UiLayer_DoesNotOwnPaginationCalculations()
    {
        var root = SolutionRootLocator.Find();
        var uiRoot = Path.Combine(root, "src", "KKL.WordStudio.UI");
        var uiSources = EnumerateProductionSourceFiles(uiRoot)
            .Select(File.ReadAllText)
            .ToList();

        Assert.DoesNotContain(uiSources, source =>
            source.Contains("MinimumTableStartDataRows", StringComparison.Ordinal)
            || source.Contains("ResolveKeepWithNextChainEndIndex", StringComparison.Ordinal)
            || source.Contains("RemoveTrailingHeadingChainWhenPageHasEarlierBodyContent", StringComparison.Ordinal));
    }

    [Fact]
    public void WordExporter_DoesNotCreateASecondTableFragmenter()
    {
        var root = SolutionRootLocator.Find();
        var word = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordContentWriter.cs");
        var tableWriter = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordTableWriter.cs");

        Assert.DoesNotContain("FragmentIndex", word, StringComparison.Ordinal);
        Assert.DoesNotContain("StartRowIndex", word, StringComparison.Ordinal);
        Assert.DoesNotContain("FragmentIndex", tableWriter, StringComparison.Ordinal);
        Assert.DoesNotContain("StartRowIndex", tableWriter, StringComparison.Ordinal);
    }

    [Fact]
    public void EngineAssembly_ContainsOneDocumentLayoutEngineImplementation()
    {
        var contract = typeof(IDocumentLayoutEngine);
        var implementations = typeof(DeterministicDocumentLayoutEngine).Assembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && contract.IsAssignableFrom(type))
            .ToList();

        var implementation = Assert.Single(implementations);
        Assert.Equal(typeof(DeterministicDocumentLayoutEngine), implementation);
    }

    private static IEnumerable<string> EnumerateProductionSourceFiles(string root) =>
        Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)));

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
