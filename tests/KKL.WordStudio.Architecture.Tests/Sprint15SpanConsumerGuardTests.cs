namespace KKL.WordStudio.Architecture.Tests;

using System.Text.RegularExpressions;
using Xunit;

public sealed class Sprint15SpanConsumerGuardTests
{
    [Fact]
    public void EnginePagination_ConsumesRowGroupsAndProjectsFragmentLocalCellSpans()
    {
        var root = SolutionRootLocator.Find();
        var files = SourceScan.ReadCodeFiles(root, "src/KKL.WordStudio.Engine/Layout", ".cs")
            .Where(file => !file.RelativePath.EndsWith("/FallbackDocumentLayoutEngine.cs", StringComparison.Ordinal))
            .ToList();
        var source = string.Join("\n", files.Select(file => file.Text));

        Assert.Contains("RowGroups", source, StringComparison.Ordinal);
        Assert.Contains("CellSpans", source, StringComparison.Ordinal);
        Assert.Contains("TableCellSpan", source, StringComparison.Ordinal);
        Assert.Matches(
            new Regex(@"TablePageBlockPayload[\s\S]{0,1800}?CellSpans\s*=", RegexOptions.CultureInvariant),
            source);
        Assert.Contains("CompositionWarnings", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_ConsumesFragmentSpansAndUsesTrueGridRowSpanInsteadOfUniformGridRows()
    {
        var root = SolutionRootLocator.Find();
        var files = ReadPreviewFiles(root);
        var source = string.Join("\n", files.Select(file => file.Text));
        var xamlPath = Path.Combine(root, "src", "KKL.WordStudio.UI", "Views", "PreviewView.xaml");
        var xaml = SourceScan.ReadWithoutComments(xamlPath);

        Assert.Contains("CellSpans", source, StringComparison.Ordinal);
        Assert.True(
            source.Contains("Grid.SetRowSpan", StringComparison.Ordinal)
            || source.Contains("Grid.RowSpan", StringComparison.Ordinal),
            "Preview table rendering must apply true WPF Grid rowspan semantics from fragment-local CellSpans.");
        Assert.Contains("PreviewTableGridControl", xaml, StringComparison.Ordinal);
        Assert.Contains("Rows=\"{Binding Rows}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CellSpans=\"{Binding CellSpans}\"", xaml, StringComparison.Ordinal);

        var dataRowUniformGridPattern = new Regex(
            @"<ItemsControl\b(?=[^>]*ItemsSource\s*=\s*""\{Binding Rows\}"")(?:(?!</ItemsControl>)[\s\S])*?<UniformGrid\b",
            RegexOptions.CultureInvariant);
        Assert.DoesNotMatch(dataRowUniformGridPattern, xaml);
    }

    [Fact]
    public void Preview_DoesNotReconstructSpanIntersectionsFromCompleteSemanticIndexes()
    {
        var root = SolutionRootLocator.Find();
        var files = ReadPreviewFiles(root);
        var offenders = SourceScan.FindMatches(
            files,
            @"intersection(Start|End)|Math\s*\.\s*(?:Max|Min)\s*\([^\)]*(?:StartRowIndex|RowIndex)[^\)]*(?:StartRowIndex|RowSpan)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Assert.True(
            offenders.Count == 0,
            "UI must consume fragment-local TablePageBlockPayload.CellSpans and must not clip complete semantic spans itself. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void WordTableWriter_ConsumesCompleteSemanticSpansAndWritesTrueVerticalMerge()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordTableWriter.cs");
        var source = SourceScan.ReadWithoutComments(path);

        Assert.Contains("tableNode.CellSpans", source, StringComparison.Ordinal);
        Assert.Contains("VerticalMerge", source, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"\bRestart\b", RegexOptions.CultureInvariant), source);
        Assert.Matches(new Regex(@"\bContinue\b", RegexOptions.CultureInvariant), source);
        Assert.Contains("new TableHeader()", source, StringComparison.Ordinal);
        Assert.Contains("TableLayoutValues.Fixed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WordExport_RemainsOnReportContentDocumentSemanticPathAndNeverConsumesLayoutResult()
    {
        var root = SolutionRootLocator.Find();
        var exporterPath = Path.Combine(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "WordExporter.cs");
        var wordFiles = SourceScan.ReadCodeFiles(root, "src/KKL.WordStudio.Infrastructure/Export/Exporters/Word", ".cs");
        var exporter = SourceScan.ReadWithoutComments(exporterPath);
        var wordSource = string.Join("\n", wordFiles.Select(file => file.Text));

        Assert.Contains("IReportContentBuilder", exporter, StringComparison.Ordinal);
        Assert.Contains("_contentBuilder.BuildAsync", exporter, StringComparison.Ordinal);
        Assert.Contains("document.BodyNodes", exporter, StringComparison.Ordinal);
        Assert.DoesNotContain("DocumentLayoutResult", exporter, StringComparison.Ordinal);
        Assert.DoesNotContain("DocumentLayoutResult", wordSource, StringComparison.Ordinal);
    }

    private static IReadOnlyList<(string RelativePath, string Text)> ReadPreviewFiles(string root) =>
        SourceScan.ReadCodeFiles(root, "src/KKL.WordStudio.UI", ".cs", ".xaml")
            .Where(file =>
                file.RelativePath.StartsWith("src/KKL.WordStudio.UI/Preview/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/ViewModels/Preview", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/Views/PreviewView", StringComparison.Ordinal))
            .ToList();
}
