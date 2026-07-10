namespace KKL.WordStudio.Domain.Projects;

using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Reports;

/// <summary>
/// The true aggregate root of the domain (see ADR 0003). A Project owns the
/// data sources imported into it and the report designs built against them.
/// A single Project can hold multiple Reports sharing the same DataSources
/// (e.g., a "Summary" and a "Detailed" report over the same workbook) — this
/// is why DataSources live here rather than on Report itself.
/// </summary>
public sealed class Project
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled Project";

    public List<DataSource> DataSources { get; } = new();
    public List<Report> Reports { get; } = new();

    /// <summary>
    /// Optional project-owned cover/preface DOCX composed before generated
    /// report content. It deliberately stays outside ReportElement: arbitrary
    /// Word content is not editable in the structured report designer.
    /// </summary>
    public FrontMatterDocument? FrontMatter { get; set; }

    /// <summary>
    /// Optional project-owned DOCX used only as a supported formatting reference.
    /// It is not front matter and is not converted into report elements.
    /// </summary>
    public ReferenceFormatDocument? ReferenceFormat { get; set; }

    public ProjectSettings Settings { get; set; } = new();
}
