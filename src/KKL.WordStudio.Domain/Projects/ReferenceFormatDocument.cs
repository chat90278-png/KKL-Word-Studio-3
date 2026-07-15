namespace KKL.WordStudio.Domain.Projects;

using System.ComponentModel;

/// <summary>
/// Reference DOCX used during the current in-memory workspace to resolve
/// supported page, text, and table formatting. It is distinct from FrontMatter
/// and is never converted into ReportElements or prepended to generated output.
/// </summary>
public sealed class ReferenceFormatDocument
{
    // Historical test/data-contract compatibility only. Runtime project open,
    // save, ZIP embedding and materialization have been removed.
    public const string DefaultEmbeddedAssetEntryName = "resources/reference-format/reference-format.docx";

    public required string FileName { get; set; }

    /// <summary>Original read-only import location selected during this session.</summary>
    public string? OriginalSourcePath { get; set; }

    /// <summary>Runtime readable path for the selected reference DOCX.</summary>
    public string? ResolvedFilePath { get; set; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string EmbeddedAssetEntryName { get; set; } = DefaultEmbeddedAssetEntryName;
}
