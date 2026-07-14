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
/// in-memory Project aggregate because the existing transfer/export pipeline
/// is project-based, but it no longer exposes native project New/Open/Save
/// commands to the user. The session lives for the lifetime of the process.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IReportExporterRegistry _exporterRegistry;
    private readonly IWorkspace _workspace;
    private readonly IFileDialogService _fileDialogService;
    private readonly IShellLauncher _shellLauncher;
    private readonly ILogger<MainViewModel> _logger;
    private readonly IDialogService? _dialogService;

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
        IProjectService projectService,
        IReportExporterRegistry exporterRegistry,
        IWorkspace workspace,
        IFileDialogService fileDialogService,
        IShellLauncher shellLauncher,
        DockViewModel dockViewModel,
        ILogger<MainViewModel> logger,
        IDialogService? dialogService = null)
    {
        ArgumentNullException.ThrowIfNull(projectService);
        _exporterRegistry = exporterRegistry;
        _workspace = workspace;
        _fileDialogService = fileDialogService;
        _shellLauncher = shellLauncher;
        DockViewModel = dockViewModel;
        _logger = logger;
        _dialogService = dialogService;

        _currentProject = projectService.CreateNew();
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

        var readiness = ReportReadinessAssessment.FromGroups(DockViewModel.Diagnostics.Groups);
        if (readiness.BlocksExport)
        {
            ReportPaneViewModel.Shared.OpenForAction();
            DockViewModel.ShowWarningsCommand.Execute(null);
            StatusText = $"Word dosyası oluşturulmadı — önce {readiness.ErrorGroupCount} kritik hatayı düzeltin.";
            _dialogService?.ShowError(
                $"Raporda {readiness.ErrorGroupCount} kritik hata grubu ({readiness.ErrorOccurrenceCount} hata bulgusu) var. Kontrol sekmesindeki hataları düzeltmeden Word dosyası oluşturulamaz.",
                "Rapor Word'e Hazır Değil");
            return;
        }

        if (readiness.RequiresWarningConfirmation
            && _dialogService is not null
            && !_dialogService.ShowConfirmation(
                $"Raporda {readiness.WarningGroupCount} uyarı grubu ({readiness.WarningOccurrenceCount} uyarı bulgusu) var. Yine de Word dosyası oluşturulsun mu?",
                "Uyarılarla Devam Et"))
        {
            StatusText = "Word dosyası oluşturma kullanıcı tarafından iptal edildi.";
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
