namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Reports;

/// <summary>
/// Projects the existing Project aggregate (DataSources, Reports, Settings)
/// into a tree — no Domain change was needed for this (see Sprint 3 ADR):
/// the shape the Project Explorer wants is already there, this ViewModel
/// just walks it. Rebuilds whenever the Workspace's active project changes.
///
/// Sprint 7 makes the tree NAVIGATIONAL instead of decorative: clicking a
/// worksheet (or a data source) activates it in the Excel Workspace —
/// opening the backing file if needed — and clicking a report makes it the
/// active report; the overlay then closes so the user lands directly on
/// what they picked. Navigation goes straight to the two ViewModels that
/// own those surfaces (both DI singletons); no event bus or mediator was
/// introduced for two method calls.
/// </summary>
public sealed partial class ProjectExplorerViewModel : ViewModelBase
{
    private readonly IWorkspace _workspace;
    private readonly ExcelWorkspaceViewModel _excelWorkspace;
    private readonly MainViewModel _main;

    public ObservableCollection<ProjectExplorerNodeViewModel> RootNodes { get; } = new();

    public ProjectExplorerViewModel(IWorkspace workspace, ExcelWorkspaceViewModel excelWorkspace, MainViewModel main)
    {
        _workspace = workspace;
        _excelWorkspace = excelWorkspace;
        _main = main;
        _workspace.WorkspaceChanged += (_, _) => Rebuild();
        Rebuild();
    }

    private void Rebuild()
    {
        RootNodes.Clear();
        var project = _workspace.ActiveProject;
        if (project is null) return;

        var dataSourcesNode = new ProjectExplorerNodeViewModel { Name = "Veri Kaynakları" };
        foreach (var dataSource in project.DataSources)
        {
            var dataSourceNode = new ProjectExplorerNodeViewModel
            {
                Name = dataSource.Name,
                OnSelected = () => NavigateToWorksheet(dataSource, worksheetName: null)
            };

            if (dataSource is ExcelDataSource excelDataSource)
            {
                foreach (var worksheet in excelDataSource.Workbook.Worksheets)
                {
                    dataSourceNode.Children.Add(new ProjectExplorerNodeViewModel
                    {
                        Name = worksheet.Name,
                        OnSelected = () => NavigateToWorksheet(dataSource, worksheet.Name)
                    });
                }
            }

            dataSourcesNode.Children.Add(dataSourceNode);
        }
        RootNodes.Add(dataSourcesNode);

        var reportsNode = new ProjectExplorerNodeViewModel { Name = "Raporlar" };
        foreach (var report in project.Reports)
        {
            reportsNode.Children.Add(new ProjectExplorerNodeViewModel
            {
                Name = report.Name,
                OnSelected = () => NavigateToReport(report)
            });
        }
        RootNodes.Add(reportsNode);

        // Templates has no backing Domain model yet (deliberately deferred — ADR 0003/0005).
        // Shown as an empty placeholder so the tree's eventual shape is visible without
        // fabricating data for a feature that doesn't exist.
        var templatesNode = new ProjectExplorerNodeViewModel { Name = "Şablonlar" };
        templatesNode.Children.Add(new ProjectExplorerNodeViewModel { Name = "(henüz yok)" });
        RootNodes.Add(templatesNode);

        RootNodes.Add(new ProjectExplorerNodeViewModel { Name = $"Ayarlar ({project.Settings.DefaultPageOrientation})" });
    }

    /// <summary>Worksheet / data source click: record the active source in the shared state, activate it in the Excel Workspace (opening the file if needed), close the overlay.</summary>
    private void NavigateToWorksheet(DataSource dataSource, string? worksheetName)
    {
        _workspace.SetActiveDataSource(dataSource.Name, worksheetName);

        if (dataSource is ExcelDataSource excelDataSource)
            _ = _excelWorkspace.NavigateToWorksheetAsync(excelDataSource.Workbook.SourcePath, worksheetName);

        _main.IsProjectExplorerOpen = false;
    }

    /// <summary>Report click: make it the active report (Contents/Preview/Properties all follow via the shared Workspace), close the overlay.</summary>
    private void NavigateToReport(Report report)
    {
        _workspace.SetActiveReport(report);
        _main.IsProjectExplorerOpen = false;
    }
}
