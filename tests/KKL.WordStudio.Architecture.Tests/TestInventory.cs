namespace KKL.WordStudio.Architecture.Tests;

using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed record TestMethodInventoryEntry(string RelativePath, string MethodName, bool IsSkipped);

internal sealed record BaselineManifest(
    int BaselineTestMethodCount,
    int BaselineTestFileCount,
    int BaselineSkippedTestCount,
    IReadOnlyList<BaselineManifestEntry> Entries);

internal sealed record BaselineManifestEntry(string RelativePath, IReadOnlyList<string> MethodNames);

internal sealed record BaselineInventoryComparison(
    int TotalTestMethods,
    IReadOnlyList<TestMethodInventoryEntry> SkippedTests,
    IReadOnlyList<string> RemovedTestFiles,
    IReadOnlyList<string> RemovedTestMethods,
    int BaselineManifestCount,
    int BaselineManifestFileCount,
    int BaselineSkippedTestCount);

internal static class TestInventory
{
    private static readonly Regex TestMethodRegex = new(
        @"\[(?<kind>Fact|Theory)(?:\s*\([^\]]*\))?\][\s\S]{0,800}?\b(?:public|internal|private|protected)\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|ValueTask(?:<[^>]+>)?|void|[A-Za-z_][\w<>,?.\[\] ]*)\s+(?<name>[A-Za-z_]\w*)\s*\(",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TestMethodInventoryEntry> Capture(string solutionRoot)
    {
        var entries = new List<TestMethodInventoryEntry>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(solutionRoot, "tests"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var source = File.ReadAllText(path);
            var relativePath = Path.GetRelativePath(solutionRoot, path).Replace('\\', '/');
            foreach (Match match in TestMethodRegex.Matches(source))
            {
                entries.Add(new TestMethodInventoryEntry(
                    relativePath,
                    match.Groups["name"].Value,
                    match.Value.Contains("Skip", StringComparison.Ordinal)));
            }
        }

        return entries;
    }

    public static BaselineInventoryComparison CompareToBaseline(string solutionRoot)
    {
        var manifestPath = Path.Combine(
            solutionRoot,
            "tests",
            "KKL.WordStudio.Architecture.Tests",
            "TestData",
            "sprint15-contract-baseline-tests.json");
        var manifest = JsonSerializer.Deserialize<BaselineManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Could not deserialize baseline test manifest: {manifestPath}");

        var manifestEntryCount = manifest.Entries.Sum(entry => entry.MethodNames.Count);
        if (manifestEntryCount != manifest.BaselineTestMethodCount)
        {
            throw new InvalidDataException(
                $"Baseline manifest count mismatch: header={manifest.BaselineTestMethodCount}, entries={manifestEntryCount}.");
        }

        if (manifest.Entries.Count != manifest.BaselineTestFileCount)
        {
            throw new InvalidDataException(
                $"Baseline manifest file-count mismatch: header={manifest.BaselineTestFileCount}, entries={manifest.Entries.Count}.");
        }

        var current = Capture(solutionRoot);
        var currentByFile = current
            .GroupBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(entry => entry.MethodName).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        var removedFiles = manifest.Entries
            .Where(entry => !File.Exists(Path.Combine(solutionRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar))))
            .Select(entry => entry.RelativePath)
            .ToList();
        var removedMethods = manifest.Entries
            .SelectMany(entry => entry.MethodNames
                .Where(method => !currentByFile.TryGetValue(entry.RelativePath, out var methods) || !methods.Contains(method))
                .Select(method => $"{entry.RelativePath}::{method}"))
            .ToList();

        return new BaselineInventoryComparison(
            current.Count,
            current.Where(entry => entry.IsSkipped).ToList(),
            removedFiles,
            removedMethods,
            manifest.BaselineTestMethodCount,
            manifest.BaselineTestFileCount,
            manifest.BaselineSkippedTestCount);
    }
}
