namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.UI.Services;
using Microsoft.Extensions.Logging;
using System.IO;

/// <summary>
/// Shell-level ViewModel: owns the active Project and pushes it into
/// IWorkspace, which every other panel reacts to. It also owns the
/// last-saved file path so "Save" does not need to re-prompt.
///
/// Deliberately does NOT fake a dirty-state indicator ("*") — no reliable
/// dirty-tracking mechanism exists yet, so the title bar simply omits it
/// rather than showing a marker that might be wrong.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IReportExporterRegistry _exporterRegistry;
    private readonly IWorkspace _workspace;
    private readonly IFileDialogService _fileDialogService;
    private readonly IShellLauncher _shellLauncher;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>Shared with ContextDockView/ContentsViewModel/PropertiesViewModel via DI (registered as a singleton) — the shell's Grid column width binds to its State.</summary>
    public DockViewModel DockViewModel { get; }

    /// <summary>Shared session-only busy state rendered by MainWindow as a full interaction shield.</summary>
    public LongOperationViewModel LongOperation { get; }

    [ObservableProperty]
    private Project _currentProject;

    [ObservableProperty]
    private string _statusText = "Hazır";

    /// <summary>Set after a successful export — enables the "Open file"/"Open folder" actions.</summary>
    [ObservableProperty]
    private string? _lastExportedFilePath;

    /// <summary>Set after a successful Open/Save — lets "Save" write back without re-prompting, while "Save As" always prompts.</summary>
    [ObservableProperty]
    private string? _currentProjectFilePath;

    public MainViewModel(
        IProjectService projectService,
        IReportExporterRegistry exporterRegistry,
        IWorkspace workspace,
        IFileDialogService fileDialogService,
        IShellLauncher shellLauncher,
        DockViewModel dockViewModel,
        LongOperationViewModel longOperation,
        ILogger<MainViewModel> logger)
    {
        _projectService = projectService;
        _exporterRegistry = exporterRegistry;
        _workspace = workspace;
        _fileDialogService = fileDialogService;
        _shellLauncher = shellLauncher;
        DockViewModel = dockViewModel;
        LongOperation = longOperation;
        _logger = logger;

        _currentProject = _projectService.CreateNew();
        _workspace.SetActiveProject(_currentProject);
        _workspace.SetActiveReport(_currentProject.Reports.FirstOrDefault());
    }

    [RelayCommand]
    private void NewProject()
    {
        CurrentProject = _projectService.CreateNew();
        _workspace.SetActiveProject(CurrentProject);
        _workspace.SetActiveReport(CurrentProject.Reports.FirstOrDefault());

        LastExportedFilePath = null;
        CurrentProjectFilePath = null;
        StatusText = $"'{CurrentProject.Name}' oluşturuldu";
        _logger.LogInformation("New project created: {ProjectId}", CurrentProject.Id);
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var path = _fileDialogService.OpenProjectFile();
        if (path is null) return;

        var result = await _projectService.OpenAsync(path);
        if (result.IsFailure)
        {
            StatusText = result.Error!;
            _logger.LogWarning("Failed to open project {Path}: {Error}", path, result.Error);
            return;
        }

        CurrentProject = result.Value;
        CurrentProjectFilePath = path;
        LastExportedFilePath = null;
        _workspace.SetActiveProject(CurrentProject);
        _workspace.SetActiveReport(CurrentProject.Reports.FirstOrDefault());

        StatusText = $"'{path}' açıldı";
        _logger.LogInformation("Opened project {ProjectId} from {Path}", CurrentProject.Id, path);
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        var path = CurrentProjectFilePath ?? _fileDialogService.SaveProjectFile(CurrentProject.Name);
        if (path is null) return;

        var result = await _projectService.SaveAsync(CurrentProject, path);
        if (result.IsSuccess) CurrentProjectFilePath = path;
        StatusText = result.IsSuccess ? $"'{path}' konumuna kaydedildi" : result.Error!;
    }

    [RelayCommand]
    private async Task SaveProjectAsAsync()
    {
        var path = _fileDialogService.SaveProjectFile(CurrentProject.Name);
        if (path is null) return;

        var result = await _projectService.SaveAsync(CurrentProject, path);
        if (result.IsSuccess) CurrentProjectFilePath = path;
        StatusText = result.IsSuccess ? $"'{path}' konumuna kaydedildi" : result.Error!;
    }

    [RelayCommand]
    private async Task ExportToWordAsync()
    {
        var report = _workspace.ActiveReport;
        if (report is null)
        {
            StatusText = "Dışa aktarılacak etkin rapor yok — önce bir rapor ekleyin veya seçin.";
            return;
        }

        var path = _fileDialogService.SaveWordFile(report.Name);
        if (path is null) return;

        var exporter = _exporterRegistry.Resolve("docx");
        var result = await exporter.ExportAsync(CurrentProject, report, ExportOptions.Default);

        if (result.IsFailure)
        {
            StatusText = result.Error!;
            LastExportedFilePath = null;
            _logger.LogWarning("Word export failed: {Error}", result.Error);
            return;
        }

        await using var fileStream = File.Create(path);
        await result.Value.CopyToAsync(fileStream);

        LastExportedFilePath = path;
        StatusText = $"'{report.Name}' raporu '{path}' konumuna aktarıldı";
        _logger.LogInformation("Exported report {ReportId} to {Path}", report.Id, path);
    }

    [RelayCommand(CanExecute = nameof(HasLastExportedFile))]
    private void OpenExportedFile() => _shellLauncher.OpenFile(LastExportedFilePath!);

    [RelayCommand(CanExecute = nameof(HasLastExportedFile))]
    private void OpenExportedFolder() => _shellLauncher.OpenContainingFolder(LastExportedFilePath!);

    private bool HasLastExportedFile() => LastExportedFilePath is not null;

    partial void OnLastExportedFilePathChanged(string? value)
    {
        OpenExportedFileCommand.NotifyCanExecuteChanged();
        OpenExportedFolderCommand.NotifyCanExecuteChanged();
    }
}
