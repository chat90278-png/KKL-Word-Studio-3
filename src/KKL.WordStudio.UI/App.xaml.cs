namespace KKL.WordStudio.UI;

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using KKL.WordStudio.Application.DependencyInjection;
using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Plugins;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Engine.DependencyInjection;
using KKL.WordStudio.Infrastructure.DependencyInjection;
using KKL.WordStudio.Infrastructure.Excel;
using KKL.WordStudio.UI.Preview;
using KKL.WordStudio.UI.Services;
using KKL.WordStudio.UI.ViewModels;
using KKL.WordStudio.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

/// <summary>
/// Composition root. This is the ONLY place in the solution allowed to
/// know about every layer simultaneously — it wires Application,
/// Infrastructure, Rendering, and UI-only services together, then hands
/// out resolved instances. No other project may do this.
/// </summary>
public partial class App : Application
{
    private const string ProductFolderName = "KKL Word Studio";
    private IHost? _host;
    private string? _logDirectory;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            ConfigureLogging();
            RegisterGlobalExceptionHandlers();
            Log.Information("Application startup entered. Base directory: {BaseDirectory}", AppContext.BaseDirectory);

            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((_, services) =>
                {
                    services.AddWordStudioApplication();
                    services.AddWordStudioEngine();
                    services.AddWordStudioInfrastructure();

                    // The Infrastructure package remains the single OpenXML
                    // implementation. This final composition-root registration
                    // decorates that implementation with shell busy state and
                    // linked cancellation; it never reads workbook bytes itself.
                    services.AddSingleton<IExcelWorkbookReader>(provider =>
                        new LongOperationExcelWorkbookReader(
                            new OpenXmlExcelWorkbookReader(
                                provider.GetRequiredService<ILogger<OpenXmlExcelWorkbookReader>>())));

                    // Last registration wins for single-service resolution. The
                    // column-selection service is still the authoritative transfer
                    // engine; the outer decorator only publishes the shell loading
                    // state around that same call.
                    services.AddSingleton<IColumnTransferSelectionSession>(
                        _ => ColumnTransferSelectionSession.Shared);
                    services.AddSingleton<IExcelReportTransferService>(provider =>
                        new LongOperationExcelReportTransferService(
                            new ColumnSelectionExcelReportTransferService(
                                provider.GetRequiredService<IColumnTransferSelectionSession>())));

                    services.AddSingleton<PreviewDiagnosticsStore>();
                    services.AddSingleton<IReportPreviewRenderer, PreviewRenderer>();

                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<IFileDialogService, FileDialogService>();
                    services.AddSingleton<IShellLauncher, ShellLauncher>();
                    services.AddSingleton<GuideImageSourceLoader>();
                    services.AddSingleton<UsageGuideViewModel>();
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
                    services.AddSingleton<UsageGuideView>();

                    services.AddSingleton<MainWindow>();

                    // Future third-party plugin modules are appended here, e.g.:
                    // services.GetRequiredService<PluginCatalog>()
                    //         .Register(new SomeThirdPartyPluginModule())
                    //         .ApplyTo(services);
                })
                .Build();

            Log.Information("Dependency-injection host built successfully.");
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            Log.Information("MainWindow resolved successfully.");

            MainWindow = mainWindow;
            mainWindow.ShowInTaskbar = true;
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.ContentRendered += MainWindow_ContentRendered;
            mainWindow.Show();
            Log.Information("MainWindow.Show completed. Handle creation and visibility recovery queued.");

            Dispatcher.BeginInvoke(
                new Action(() => EnsureMainWindowVisible(mainWindow)),
                DispatcherPriority.ApplicationIdle);
        }
        catch (Exception exception)
        {
            ReportFatalStartupFailure("OnStartup", exception);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exit requested with code {ExitCode}.", e.ApplicationExitCode);
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void ConfigureLogging()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductFolderName,
            "Logs");
        Directory.CreateDirectory(_logDirectory);

        var logPath = Path.Combine(_logDirectory, "wordstudio-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, eventArgs) =>
        {
            ReportFatalStartupFailure("DispatcherUnhandledException", eventArgs.Exception);
            eventArgs.Handled = true;
            Shutdown(-1);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            var exception = eventArgs.ExceptionObject as Exception
                ?? new InvalidOperationException(eventArgs.ExceptionObject?.ToString() ?? "Unknown AppDomain failure.");
            WriteEmergencyLog("AppDomain.UnhandledException", exception);
            Log.Fatal(exception, "Unhandled AppDomain exception. Terminating: {IsTerminating}", eventArgs.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            Log.Error(eventArgs.Exception, "Unobserved task exception.");
            eventArgs.SetObserved();
        };
    }

    private void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        if (sender is not MainWindow mainWindow)
            return;

        mainWindow.ContentRendered -= MainWindow_ContentRendered;
        EnsureMainWindowVisible(mainWindow);
        Log.Information(
            "Main window rendered. Left={Left}, Top={Top}, Width={Width}, Height={Height}, State={State}, Visible={Visible}",
            mainWindow.Left,
            mainWindow.Top,
            mainWindow.ActualWidth,
            mainWindow.ActualHeight,
            mainWindow.WindowState,
            mainWindow.IsVisible);
    }

    private static void EnsureMainWindowVisible(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        const double visibleMargin = 80;

        var coordinatesAreFinite = double.IsFinite(window.Left)
                                   && double.IsFinite(window.Top)
                                   && double.IsFinite(width)
                                   && double.IsFinite(height);
        var intersectsVisibleDesktop = coordinatesAreFinite
                                       && window.Left < virtualRight - visibleMargin
                                       && window.Left + width > virtualLeft + visibleMargin
                                       && window.Top < virtualBottom - visibleMargin
                                       && window.Top + height > virtualTop + visibleMargin;

        if (!intersectsVisibleDesktop)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = virtualLeft + Math.Max(0, (SystemParameters.VirtualScreenWidth - width) / 2);
            window.Top = virtualTop + Math.Max(0, (SystemParameters.VirtualScreenHeight - height) / 2);
        }

        window.ShowInTaskbar = true;
        if (!window.IsVisible)
            window.Show();

        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    private void ReportFatalStartupFailure(string stage, Exception exception)
    {
        WriteEmergencyLog(stage, exception);
        try
        {
            Log.Fatal(exception, "Fatal application failure during {Stage}.", stage);
            Log.CloseAndFlush();
        }
        catch
        {
            // The emergency text log below is the last-resort diagnostic path.
        }

        var diagnosticPath = ResolveEmergencyLogPath();
        try
        {
            MessageBox.Show(
                $"KKL Word Studio başlatılamadı.\n\n{exception.Message}\n\nTanılama dosyası:\n{diagnosticPath}",
                "KKL Word Studio — Başlangıç Hatası",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Do not hide the original failure if WPF cannot create a dialog.
        }
    }

    private void WriteEmergencyLog(string stage, Exception exception)
    {
        try
        {
            var path = ResolveEmergencyLogPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var entry = new StringBuilder()
                .AppendLine($"[{DateTimeOffset.Now:O}] {stage}")
                .AppendLine(exception.ToString())
                .AppendLine(new string('-', 80))
                .ToString();
            File.AppendAllText(path, entry);
        }
        catch
        {
            // This is deliberately best-effort and must never mask the failure.
        }
    }

    private string ResolveEmergencyLogPath()
    {
        var directory = _logDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductFolderName,
                "Logs");
        }

        return Path.Combine(directory, "startup-failures.log");
    }
}
