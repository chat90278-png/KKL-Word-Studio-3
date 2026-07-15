namespace KKL.WordStudio.Domain.Projects;

/// <summary>
/// Reference DOCX used during the current in-memory workspace to resolve
/// supported page, text, and table formatting. It is distinct from FrontMatter
/// and is never converted into ReportElements or prepended to generated output.
/// </summary>
public sealed class ReferenceFormatDocument
{
    public required string FileName { get; set; }

    /// <summary>Original read-only import location selected during this session.</summary>
    public string? OriginalSourcePath { get; set; }

    /// <summary>Runtime readable path for the selected reference DOCX.</summary>
    public string? ResolvedFilePath { get; set; }
}
