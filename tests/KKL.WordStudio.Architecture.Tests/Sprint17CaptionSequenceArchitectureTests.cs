namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint17CaptionSequenceArchitectureTests
{
    [Fact]
    public void EngineAndWord_KeepDocumentOrderCaptionCountersOnStructuredSequenceMetadata()
    {
        var root = SolutionRootLocator.Find();
        var formatter = Read(root, "src", "KKL.WordStudio.Application", "Formatting", "TableCaptionSequenceFormatter.cs");
        var generatedPaginator = Read(root, "src", "KKL.WordStudio.Engine", "Layout", "GeneratedDocumentPaginator.cs");
        var tablePaginator = Read(root, "src", "KKL.WordStudio.Engine", "Layout", "DeterministicTablePaginator.cs");
        var wordExporter = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "WordExporter.cs");
        var wordContentWriter = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordContentWriter.cs");
        var wordParagraphWriter = Read(root, "src", "KKL.WordStudio.Infrastructure", "Export", "Exporters", "Word", "WordParagraphWriter.cs");

        Assert.Contains("ResolveNextSequenceNumber", formatter, StringComparison.Ordinal);
        Assert.Contains("SequenceIdentifier", formatter, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(caption)", formatter, StringComparison.Ordinal);

        Assert.Contains("captionSequenceCounters = new Dictionary<string, int>(StringComparer.Ordinal)", generatedPaginator, StringComparison.Ordinal);
        Assert.Contains("ResolveCaptionSequenceNumber(table, captionSequenceCounters)", generatedPaginator, StringComparison.Ordinal);
        Assert.Contains("CaptionSequenceNumber = captionSequenceNumber", tablePaginator, StringComparison.Ordinal);
        Assert.Contains("TableCaptionSequenceFormatter.BuildDisplayText", tablePaginator, StringComparison.Ordinal);

        Assert.Contains("captionSequenceCounters = new Dictionary<string, int>(StringComparer.Ordinal)", wordExporter, StringComparison.Ordinal);
        Assert.Contains("WordContentWriter.AppendNode(body, node, captionSequenceCounters)", wordExporter, StringComparison.Ordinal);
        Assert.Contains("TableCaptionSequenceFormatter.ResolveNextSequenceNumber", wordContentWriter, StringComparison.Ordinal);
        Assert.Contains("cachedSequenceNumber", wordParagraphWriter, StringComparison.Ordinal);
        Assert.Contains("new SimpleField", wordParagraphWriter, StringComparison.Ordinal);
        Assert.Contains("SEQ {sequence.SequenceIdentifier}", wordParagraphWriter, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewNumbering_UsesPayloadMetadataAndNeverHardcodesTabloOneInXaml()
    {
        var root = SolutionRootLocator.Find();
        var projection = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewPageProjection.cs");
        var xaml = Read(root, "src", "KKL.WordStudio.UI", "Views", "PreviewView.xaml");

        Assert.Contains("TableCaptionSequenceFormatter.BuildDisplayText", projection, StringComparison.Ordinal);
        Assert.Contains("table.CaptionSequence", projection, StringComparison.Ordinal);
        Assert.Contains("table.CaptionSequenceNumber", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("Tablo 1:", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Tablo 2:", xaml, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts)
    {
        var pathParts = new string[parts.Length + 1];
        pathParts[0] = root;
        Array.Copy(parts, 0, pathParts, 1, parts.Length);
        return File.ReadAllText(Path.Combine(pathParts));
    }
}
