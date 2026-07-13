namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint22StartupDiagnosticsArchitectureTests
{
    [Fact]
    public void Startup_RecordsStagesAndSurfacesFatalFailures()
    {
        var root = SolutionRootLocator.Find();
        var app = File.ReadAllText(Path.Combine(root, "src", "KKL.WordStudio.UI", "App.xaml.cs"));

        Assert.Contains("Environment.SpecialFolder.LocalApplicationData", app, StringComparison.Ordinal);
        Assert.Contains("wordstudio-.log", app, StringComparison.Ordinal);
        Assert.Contains("startup-failures.log", app, StringComparison.Ordinal);
        Assert.Contains("Application startup entered", app, StringComparison.Ordinal);
        Assert.Contains("Dependency-injection host built successfully", app, StringComparison.Ordinal);
        Assert.Contains("MainWindow resolved successfully", app, StringComparison.Ordinal);
        Assert.Contains("DispatcherUnhandledException", app, StringComparison.Ordinal);
        Assert.Contains("AppDomain.CurrentDomain.UnhandledException", app, StringComparison.Ordinal);
        Assert.Contains("TaskScheduler.UnobservedTaskException", app, StringComparison.Ordinal);
        Assert.Contains("MessageBox.Show", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_RecoversMainWindowOntoVisibleDesktop()
    {
        var root = SolutionRootLocator.Find();
        var app = File.ReadAllText(Path.Combine(root, "src", "KKL.WordStudio.UI", "App.xaml.cs"));

        Assert.Contains("MainWindow = mainWindow", app, StringComparison.Ordinal);
        Assert.Contains("EnsureMainWindowVisible", app, StringComparison.Ordinal);
        Assert.Contains("SystemParameters.VirtualScreenLeft", app, StringComparison.Ordinal);
        Assert.Contains("SystemParameters.VirtualScreenWidth", app, StringComparison.Ordinal);
        Assert.Contains("window.WindowState = WindowState.Normal", app, StringComparison.Ordinal);
        Assert.Contains("window.ShowInTaskbar = true", app, StringComparison.Ordinal);
        Assert.Contains("window.Activate()", app, StringComparison.Ordinal);
        Assert.Contains("window.Topmost = true", app, StringComparison.Ordinal);
        Assert.Contains("window.Topmost = false", app, StringComparison.Ordinal);
    }
}
