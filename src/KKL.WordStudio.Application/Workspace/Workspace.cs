namespace KKL.WordStudio.Application.Workspace;

using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;

public sealed class Workspace : IWorkspace
{
    public Project? ActiveProject { get; private set; }
    public Report? ActiveReport { get; private set; }
    public string? ActiveDataSourceName { get; private set; }
    public string? SelectedWorksheetName { get; private set; }
    public Guid? SelectedReportElementId { get; private set; }
    public bool IsPreviewActive { get; private set; }

    public event EventHandler? WorkspaceChanged;
    public event EventHandler? ReportContentChanged;

    public void SetActiveProject(Project? project)
    {
        ActiveProject = project;
        ActiveReport = null;
        ActiveDataSourceName = null;
        SelectedWorksheetName = null;
        SelectedReportElementId = null;
        IsPreviewActive = false;
        RaiseWorkspaceChanged();
        RaiseReportContentChanged(); // switching projects always changes what should be previewed/exported
    }

    public void SetActiveReport(Report? report)
    {
        ActiveReport = report;
        SelectedReportElementId = null;
        RaiseWorkspaceChanged();
        RaiseReportContentChanged(); // switching reports always changes what should be previewed/exported
    }

    public void SetActiveDataSource(string? dataSourceName, string? worksheetName)
    {
        ActiveDataSourceName = dataSourceName;
        SelectedWorksheetName = worksheetName;
        RaiseWorkspaceChanged();
    }

    public void SetSelectedReportElement(Guid? reportElementId)
    {
        // Sprint 7: Contents, Preview and Properties all synchronize through
        // this one shared selection state. When Preview selects an element,
        // Contents mirrors it, and the TreeView's own selection event then
        // reports the SAME element back here — without this guard that echo
        // would re-raise WorkspaceChanged and ping-pong between the panels.
        if (SelectedReportElementId == reportElementId) return;

        SelectedReportElementId = reportElementId;
        RaiseWorkspaceChanged();
        // Deliberately does NOT raise ReportContentChanged — selecting an
        // element in a tree does not change what the report contains.
    }

    public void SetPreviewActive(bool isActive)
    {
        IsPreviewActive = isActive;
        RaiseWorkspaceChanged();
    }

    public void NotifyReportContentChanged() => RaiseReportContentChanged();

    private void RaiseWorkspaceChanged() => WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    private void RaiseReportContentChanged() => ReportContentChanged?.Invoke(this, EventArgs.Empty);
}
