namespace KKL.WordStudio.Infrastructure.Word;

using KKL.WordStudio.Domain.Projects;

/// <summary>
/// Infrastructure-owned file-system resolution for front-matter state. Domain
/// keeps metadata/runtime state only and never performs I/O.
/// </summary>
internal static class FrontMatterSourcePathResolver
{
    public static string? Resolve(FrontMatterDocument? frontMatter)
    {
        if (!string.IsNullOrWhiteSpace(frontMatter?.ResolvedFilePath) && File.Exists(frontMatter.ResolvedFilePath))
            return frontMatter.ResolvedFilePath;
        if (!string.IsNullOrWhiteSpace(frontMatter?.OriginalSourcePath) && File.Exists(frontMatter.OriginalSourcePath))
            return frontMatter.OriginalSourcePath;
        return null;
    }
}
