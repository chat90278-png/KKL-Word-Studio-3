namespace KKL.WordStudio.Architecture.Tests;

using System.Text.RegularExpressions;
using Xunit;

public sealed class Sprint15OrchestrationAndHeuristicGuardTests
{
    private static readonly string[] MatchKeyAliases =
    [
        "PN", "P/N", "Part No", "Part Number", "Product No", "Product Number",
        "Parça Numarası", "Parca Numarasi", "Ürün No", "Urun No"
    ];

    private static readonly string[] SerialAliases =
    [
        "Serial No", "Serial Number", "Seri No", "Seri Numarası", "Seri Numarasi", "S/N", "SN"
    ];

    private static readonly string[] QuantityAliases =
    [
        "Quantity", "Qty", "Adet", "Miktar"
    ];

    [Fact]
    public void ReportContentBuilder_UsesComposerAsSingleSuccessfulTableCompositionBoundary()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.Application", "Content", "ReportContentBuilder.cs");
        var source = SourceScan.ReadWithoutComments(path);

        Assert.Contains("private readonly ITableContentRowComposer _tableContentRowComposer;", source, StringComparison.Ordinal);
        Assert.Single(
            Regex.Matches(
                    source,
                    @"\b_tableContentRowComposer\s*\.\s*Compose\s*\(",
                    RegexOptions.CultureInvariant)
                .Cast<Match>());
        Assert.Equal(
            3,
            Regex.Matches(source, @"\breturn\s+BuildComposedTableNode\s*\(", RegexOptions.CultureInvariant).Count);
        Assert.Matches(
            new Regex(
                @"BuildComposedTableNode\s*\([\s\S]{0,700}?_tableContentRowComposer\s*\.\s*Compose\s*\(\s*table\s*,\s*normalizedRows\s*\)[\s\S]{0,700}?Rows\s*=\s*composition\.Rows[\s\S]{0,350}?CellSpans\s*=\s*composition\.CellSpans[\s\S]{0,350}?RowGroups\s*=\s*composition\.RowGroups[\s\S]{0,350}?CompositionWarnings\s*=\s*composition\.Warnings",
                RegexOptions.CultureInvariant),
            source);
    }

    [Fact]
    public void ReportContentBuilder_MultiSourceRowsAreNormalizedAndAppendedBeforeComposition()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.Application", "Content", "ReportContentBuilder.cs");
        var source = SourceScan.ReadWithoutComments(path);
        var methodStart = source.IndexOf("private async Task<TableContentNode> BuildMultiSourceTableNodeAsync", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "BuildMultiSourceTableNodeAsync was not found.");

        var sourceLoop = source.IndexOf("foreach (var source in table.Sources)", methodStart, StringComparison.Ordinal);
        var normalizedAppend = source.IndexOf("renderedRows.Add(mappings", methodStart, StringComparison.Ordinal);
        var composeReturn = source.IndexOf("return BuildComposedTableNode(", methodStart, StringComparison.Ordinal);

        Assert.True(sourceLoop >= methodStart, "Multi-source composition must iterate ordered table.Sources.");
        Assert.True(normalizedAppend > sourceLoop, "Source-specific mappings must normalize rows before append.");
        Assert.True(composeReturn > normalizedAppend, "The composer must run only after normalized rows from all sources are appended.");
    }

    [Fact]
    public void ReportContentBuilder_SourceErrorPathDoesNotComposePartialRowsOrEmbedGroupingAliases()
    {
        var root = SolutionRootLocator.Find();
        var path = Path.Combine(root, "src", "KKL.WordStudio.Application", "Content", "ReportContentBuilder.cs");
        var source = SourceScan.ReadWithoutComments(path);
        var errorSegment = ExtractSegment(
            source,
            "private TableContentNode BuildMultiSourceErrorNode",
            "private static IEnumerable<IReadOnlyDictionary<string, object?>> ApplySourceSort");

        Assert.DoesNotContain("Compose(", errorSegment, StringComparison.Ordinal);
        Assert.Contains("SourceError = error", errorSegment, StringComparison.Ordinal);

        var aliases = FindRoleAliasFamilies(source);
        Assert.Empty(aliases);
    }

    [Fact]
    public void ApplicationTableComposition_IsOnlyProductionAreaAllowedToContainFullGroupingAliasSets()
    {
        var root = SolutionRootLocator.Find();
        var files = SourceScan.ReadCodeFiles(root, "src", ".cs");
        var offenders = files
            .Where(file => !file.RelativePath.StartsWith("src/KKL.WordStudio.Application/TableComposition/", StringComparison.Ordinal))
            .Select(file => (file.RelativePath, Families: FindRoleAliasFamilies(file.Text)))
            .Where(item => item.Families.Count == 3)
            .Select(item => $"{item.RelativePath} [{string.Join(", ", item.Families)}]")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A complete Product/Serial/Quantity alias heuristic set is allowed only under Application/TableComposition. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void EnginePreviewAndWord_DoNotReDeriveGroupingFromHeaderAliases()
    {
        var root = SolutionRootLocator.Find();
        var productionFiles = SourceScan.ReadCodeFiles(root, "src", ".cs", ".xaml")
            .Where(file =>
                file.RelativePath.StartsWith("src/KKL.WordStudio.Engine/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/Preview/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/ViewModels/Preview", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/Views/PreviewView", StringComparison.Ordinal)
                || file.RelativePath.Contains("/Export/Exporters/Word/", StringComparison.Ordinal)
                || file.RelativePath.EndsWith("/Export/Exporters/WordExporter.cs", StringComparison.Ordinal))
            .ToList();

        var offenders = productionFiles
            .Select(file => (file.RelativePath, Families: FindRoleAliasFamilies(file.Text)))
            .Where(item => item.Families.Count > 0)
            .Select(item => $"{item.RelativePath} [{string.Join(", ", item.Families)}]")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Engine/Preview/Word must consume semantic grouping metadata and must not match Product/Serial/Quantity aliases. Offenders: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void NonCompositionConsumers_DoNotContainQuantityDrivenRowExplosionLoops()
    {
        var root = SolutionRootLocator.Find();
        var files = SourceScan.ReadCodeFiles(root, "src", ".cs")
            .Where(file =>
                file.RelativePath.StartsWith("src/KKL.WordStudio.Engine/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/Preview/", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("src/KKL.WordStudio.UI/ViewModels/Preview", StringComparison.Ordinal)
                || file.RelativePath.Contains("/Export/Exporters/Word/", StringComparison.Ordinal)
                || file.RelativePath.EndsWith("/Content/ReportContentBuilder.cs", StringComparison.Ordinal))
            .ToList();

        var pattern = @"Enumerable\s*\.\s*(?:Range|Repeat)\s*\([^\)]*quantit|for\s*\([^;]*;[^;]*(?:quantity|adet|qty)";
        var offenders = SourceScan.FindMatches(files, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Assert.True(
            offenders.Count == 0,
            "Only the semantic row composer may expand exact serial groups; quantity-driven row generation was found in: " +
            string.Join(", ", offenders));
    }

    private static string ExtractSegment(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Source marker not found: {startMarker}");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Source end marker not found after {startMarker}: {endMarker}");
        return source[start..end];
    }

    private static IReadOnlyList<string> FindRoleAliasFamilies(string source)
    {
        var literals = Regex.Matches(source, "\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => match.Value[1..^1])
            .Select(NormalizeAlias)
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var families = new List<string>();
        if (MatchKeyAliases.Select(NormalizeAlias).Any(literals.Contains)) families.Add("match-key");
        if (SerialAliases.Select(NormalizeAlias).Any(literals.Contains)) families.Add("serial");
        if (QuantityAliases.Select(NormalizeAlias).Any(literals.Contains)) families.Add("quantity");
        return families;
    }

    private static string NormalizeAlias(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
