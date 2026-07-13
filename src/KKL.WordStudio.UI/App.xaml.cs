namespace KKL.WordStudio.UI;

using System.Windows;
using KKL.WordStudio.Application.DependencyInjection;
using KKL.WordStudio.Application.Plugins;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Engine.DependencyInjection;
using KKL.WordStudio.Infrastructure.DependencyInjection;
using KKL.WordStudio.UI.Preview;
using KKL.WordStudio.UI.Services;
using KKL.WordStudio.UI.ViewModels;
using KKL.WordStudio.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

/// <summary>
/// Composition root. This is the ONLY place in the solution allowed to
/// know about every layer simultaneously — it wires Application,
/// Infrastructure, Rendering, and UI-only services together, then hands
/// out resolved instances. No other project may do this.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/wordstudio-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddWordStudioApplication();
                services.AddWordStudioEngine();
                services.AddWordStudioInfrastructure();

                services.AddSingleton<PreviewDiagnosticsStore>();
                services.AddSingleton<IReportPreviewRenderer, PreviewRenderer>();

                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IFileDialogService, FileDialogService>();
                services.AddSingleton<IShellLauncher, ShellLauncher>();
                services.AddSingleton<MainViewModel>();

                services.AddSingleton<ExcelWorkspaceViewModel>();
                services.AddSingleton<ExcelWorkspaceView>();
                services.AddSingleton<QuickAssemblyViewModel>();
                services.AddSingleton<QuickAssemblyView>();
                services.AddSingleton<LoadedSourcesView>();

                // Shared by MainWindow (column width), ContextDockView, and every
                // dock page ViewModel — registered once as a singleton so they
                // all observe the exact same dock state.
                services.AddSingleton<DockViewModel>();

                services.AddSingleton<ContentsViewModel>();
                services.AddSingleton<ContentsView>();

                services.AddSingleton<PropertiesViewModel>();
                services.AddSingleton<PropertiesView>();
                services.AddSingleton<ChangeBindingView>();

                services.AddSingleton<PreviewViewModel>();
                services.AddSingleton<PreviewView>();

                services.AddSingleton<WarningCenterViewModel>();
                services.AddSingleton<WarningCenterView>();
                services.AddSingleton<ContextDockView>();

                services.AddSingleton<MainWindow>();

                // Future third-party plugin modules are appended here, e.g.:
                // services.GetRequiredService<PluginCatalog>()
                //         .Register(new SomeThirdPartyPluginModule())
                //         .ApplyTo(services);
            })
            .Build();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
