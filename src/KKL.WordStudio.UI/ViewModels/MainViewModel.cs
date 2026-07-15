namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.UI.Services;
using Microsoft.Extensions.Logging;
using System.IO;

/// <summary>
/// Shell-level ViewModel for the Excel-first session. It bootstraps one
/// process-lifetime in-memory Project aggregate as the internal report and
/// transfer container. Native project files and project lifecycle services are
/// not part of this product flow.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IReportExporterRegistry _exporterRegistry;
    private readonly IWorkspace _workspace;
    private readonly IFileDialogService _fileDialogService;
    private readonly IShellLauncher _shellLauncher;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>Shared with ContextDockView/ContentsViewModel/PropertiesViewModel via DI (registered as a singleton) — the shell's Grid column width binds to its State.</summary>
    public DockViewModel DockViewModel { get; }

    /// <summary>Shared session-only busy state rendered by MainWindow as a full interaction shield.</summary>
    public LongOperationViewModel LongOperation { get; } = LongOperationViewModel.Shared;

    [ObservableProperty]
    private Project _currentProject;

    [ObservableProperty]
    private string _statusText = "Hazır";

    /// <summary>Set after a successful export — enables the "Open file"/"Open folder" actions.</summary>
    [ObservableProperty]
    private string? _lastExportedFilePath;

    public MainViewModel(
        IReportExporterRegistry exporterRegistry,
        IWorkspace workspace,
        IFileDialogService fileDialogService,
        IShellLauncher shellLauncher,
        DockViewModel dockViewModel,
        ILogger<MainViewModel> logger)
    {
        _exporterRegistry = exporterRegistry;
        _workspace = workspace;
        _fileDialogService = fileDialogService;
        _shellLauncher = shellLauncher;
        DockViewModel = dockViewModel;
        _logger = logger;

        _currentProject = WorkspaceSessionFactory.CreateDefault();
        _workspace.SetActiveProject(_currentProject);
        _workspace.SetActiveReport(_currentProject.Reports.FirstOrDefault());
        _logger.LogInformation(
            "Excel-first workspace session initialized: {ProjectId}",
            _currentProject.Id);
    }

    [RelayCommand]
    private async Task ExportToWordAsync()
    {
        var report = _workspace.ActiveReport;
        if (report is null)
        {
            StatusText = "Word dosyası oluşturulamadı — etkin rapor çalışma alanı hazır değil.";
            return;
        }

        var preflight = WordExportPreflightPolicy.Evaluate(DockViewModel.Diagnostics.Groups);
        if (!preflight.CanExport)
        {
            ReportPaneViewModel.Shared.OpenForAction();
            DockViewModel.ShowBlockingErrors();
            StatusText = $"Word dosyası oluşturulmadı — {preflight.ErrorGroupCount} engelleyici sorun türü · {preflight.ErrorFindingCount} açık hata var. Uyarılar sekmesinden düzeltin.";
            _logger.LogWarning(
                "Word export blocked by diagnostics: {ErrorGroups} groups, {ErrorFindings} findings",
                preflight.ErrorGroupCount,
                preflight.ErrorFindingCount);
            return;
        }

        // File selection happens only after readiness has been decided. A blocked
        // export never opens a Save dialog or invokes the exporter.
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
        StatusText = BuildExportSuccessStatus(report.Name, path, preflight);
        _logger.LogInformation(
            "Exported report {ReportId} to {Path} with {DiagnosticGroups} non-blocking groups and {DiagnosticFindings} findings",
            report.Id,
            path,
            preflight.NonBlockingGroupCount,
            preflight.NonBlockingFindingCount);
    }

    private static string BuildExportSuccessStatus(
        string reportName,
        string path,
        WordExportPreflightResult preflight)
    {
        var baseText = $"'{reportName}' raporu '{path}' konumuna aktarıldı";
        return preflight.Status == WordExportPreflightStatus.ReadyWithFindings
            ? $"{baseText} · {preflight.NonBlockingGroupCount} sorun türü / {preflight.NonBlockingFindingCount} açık bulgu ile"
            : baseText;
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
