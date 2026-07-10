namespace KKL.WordStudio.Infrastructure.ReferenceFormatting;

using KKL.WordStudio.Domain.Projects;

internal static class ReferenceFormatSourcePathResolver
{
    public static string? Resolve(ReferenceFormatDocument? referenceFormat)
    {
        if (referenceFormat is null)
            return null;

        if (!string.IsNullOrWhiteSpace(referenceFormat.ResolvedFilePath)
            && File.Exists(referenceFormat.ResolvedFilePath))
        {
            return referenceFormat.ResolvedFilePath;
        }

        if (!string.IsNullOrWhiteSpace(referenceFormat.OriginalSourcePath)
            && File.Exists(referenceFormat.OriginalSourcePath))
        {
            return referenceFormat.OriginalSourcePath;
        }

        return null;
    }
}
