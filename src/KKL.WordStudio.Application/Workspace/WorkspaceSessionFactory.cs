namespace KKL.WordStudio.Application.Workspace;

using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;

/// <summary>
/// Creates the one process-lifetime report workspace used by the Excel-first
/// product flow. This factory owns no file format, repository, recent-file,
/// open, save, or restore behavior.
/// </summary>
public static class WorkspaceSessionFactory
{
    public static Project CreateDefault()
    {
        var project = new Project { Name = "Çalışma Oturumu" };
        var report = new Report { Name = "Rapor 1" };
        var page = new Page();
        page.Sections.Add(new Section { Name = "Body", Kind = SectionKind.Body });
        report.Pages.Add(page);
        project.Reports.Add(report);
        return project;
    }
}
