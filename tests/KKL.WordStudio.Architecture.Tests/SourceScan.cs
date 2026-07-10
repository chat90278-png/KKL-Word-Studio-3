namespace KKL.WordStudio.Architecture.Tests;

using System.Text.RegularExpressions;

internal static partial class SourceScan
{
    public static IReadOnlyList<(string RelativePath, string Text)> ReadCodeFiles(string root, string relativeDirectory, params string[] extensions)
    {
        var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory))
            return Array.Empty<(string, string)>();

        var extensionSet = extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Directory
            .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => extensionSet.Contains(Path.GetExtension(path)))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .Select(path => (
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                StripComments(File.ReadAllText(path))))
            .ToList();
    }

    public static IReadOnlyList<string> FindMatches(
        IReadOnlyList<(string RelativePath, string Text)> files,
        string pattern,
        RegexOptions options = RegexOptions.None) =>
        files
            .Where(file => Regex.IsMatch(file.Text, pattern, options | RegexOptions.CultureInvariant))
            .Select(file => file.RelativePath)
            .ToList();

    public static string ReadWithoutComments(string path) => StripComments(File.ReadAllText(path));

    private static string StripComments(string text) => CommentRegex().Replace(text, string.Empty);

    [GeneratedRegex(@"//.*?$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CommentRegex();
}
