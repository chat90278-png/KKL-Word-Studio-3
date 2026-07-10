namespace KKL.WordStudio.Domain.Projects;

using System.Text.Json.Serialization;

/// <summary>
/// Project-owned Word front-matter source (cover/preface). This is NOT a
/// ReportElement and is never converted into the KKL design model: it is an
/// imported document asset composed before generated report content at Word
/// export time.
/// </summary>
public sealed class FrontMatterDocument
{
    public const string DefaultEmbeddedAssetEntryName = "resources/frontmatter/front-matter.docx";

    public required string FileName { get; set; }

    /// <summary>
    /// Informational original import location. The .kws persistence layer owns
    /// a copy of the DOCX as a separate ZIP asset, so portability does not
    /// depend on this path continuing to exist.
    /// </summary>
    public string? OriginalSourcePath { get; set; }

    /// <summary>Relative ZIP entry used by .kws persistence for the project-owned imported asset.</summary>
    public string EmbeddedAssetEntryName { get; set; } = DefaultEmbeddedAssetEntryName;

    /// <summary>
    /// Runtime-only readable path. On initial import this is the original file;
    /// on project open the persistence layer materializes the embedded asset to
    /// a private temp location. Never serialized into project.json. File-system
    /// availability checks deliberately remain outside Domain.
    /// </summary>
    [JsonIgnore]
    public string? ResolvedFilePath { get; set; }
}
