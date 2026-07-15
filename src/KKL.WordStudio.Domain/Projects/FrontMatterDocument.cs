namespace KKL.WordStudio.Domain.Projects;

/// <summary>
/// Word front-matter source (cover/preface) used by the current in-memory
/// workspace. This is not a ReportElement and is never converted into the KKL
/// design model; it is composed before generated report content at Word export.
/// </summary>
public sealed class FrontMatterDocument
{
    public required string FileName { get; set; }

    /// <summary>Original read-only import location selected during this session.</summary>
    public string? OriginalSourcePath { get; set; }

    /// <summary>
    /// Runtime readable path. It normally equals the original import path; file
    /// system availability checks deliberately remain outside Domain.
    /// </summary>
    public string? ResolvedFilePath { get; set; }
}
