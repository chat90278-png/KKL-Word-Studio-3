namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint24PaginationParityArchitectureTests
{
    [Fact]
    public void PreviewAndWord_ConsumeOneSemanticPaginationPolicy()
    {
        var root = SolutionRootLocator.Find();
        var policy = Read(root, "src", "KKL.WordStudio.Application", "Content", "ReportFlowPaginationPolicy.cs");
        var preview = Read(root, "src", "KKL.WordStudio.Engine", "Layout", "LayoutPageFlow.cs");
        var word = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordContentWriter.cs");

        Assert.Contains("ParticipatesInHeadingChain", policy, StringComparison.Ordinal);
        Assert.Contains("KeepTableCaptionWithTable", policy, StringComparison.Ordinal);
        Assert.Contains("KeepTableRowsIntact", policy, StringComparison.Ordinal);
        Assert.Contains("RemoveTrailingHeadingChainWhenPageHasEarlierBodyContent", preview, StringComparison.Ordinal);
        Assert.Contains("ReportFlowPaginationPolicy.IsHeading", preview, StringComparison.Ordinal);
        Assert.Contains("new KeepNext()", word, StringComparison.Ordinal);
        Assert.Contains("new CantSplit()", word, StringComparison.Ordinal);
        Assert.Contains("WordTableWriter.BuildTable(table)", word, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildPageBreakParagraph", word, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
