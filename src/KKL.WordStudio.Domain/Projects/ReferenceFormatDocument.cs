namespace KKL.WordStudio.Domain.Projects;

using System.Text.Json.Serialization;

/// <summary>
/// Project-owned reference DOCX used only to resolve supported page, text,
/// and table formatting. It is distinct from FrontMatter and is never
/// converted into ReportElements or prepended to generated output.
/// </summary>
public sealed class ReferenceFormatDocument
{
    public const string DefaultEmbeddedAssetEntryName = "resources/reference-format/reference-format.docx";

    public required string FileName { get; set; }

    /// <summary>Informational original import location; project portability relies on the embedded .kws asset.</summary>
    public string? OriginalSourcePath { get; set; }

    /// <summary>Relative .kws ZIP entry containing the project-owned reference DOCX.</summary>
    public string EmbeddedAssetEntryName { get; set; } = DefaultEmbeddedAssetEntryName;

    /// <summary>Runtime-only readable path for the imported or materialized reference DOCX.</summary>
    [JsonIgnore]
    public string? ResolvedFilePath { get; set; }
}
