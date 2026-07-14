namespace KKL.WordStudio.Architecture.Tests;

using KKL.WordStudio.Application.Preview;
using Xunit;

public sealed class Sprint20DiagnosticsArchitectureTests
{
    [Fact]
    public void PreviewSnapshot_FrozenContractRemainsUntouchedByRuntimeDiagnostics()
    {
        Assert.Null(typeof(PreviewSnapshot).GetProperty("Diagnostics"));
    }

    [Fact]
    public void ContextDock_HostsWarningsBesideContentsAndProperties()
    {
        var root = SolutionRootLocator.Find();
        var dock = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContextDockView.xaml");
        var dockCode = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContextDockView.xaml.cs");
        var dockState = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "DockState.cs");
        var app = Read(root, "src", "KKL.WordStudio.UI", "App.xaml.cs");

        Assert.Contains("Content=\"İçindekiler\"", dock, StringComparison.Ordinal);
        Assert.Contains("Content=\"Özellikler\"", dock, StringComparison.Ordinal);
        Assert.Contains("Text=\"Kontrol\"", dock, StringComparison.Ordinal);
        Assert.Contains("WarningsHost", dock, StringComparison.Ordinal);
        Assert.Contains("ConverterParameter=Warnings", dock, StringComparison.Ordinal);
        Assert.Contains("DockPage { Contents, Properties, Warnings, ChangeBinding }", dockState, StringComparison.Ordinal);
        Assert.Contains("WarningsHost.Content = warningCenterView", dockCode, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<PreviewDiagnosticsStore>()", app, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<WarningCenterViewModel>()", app, StringComparison.Ordinal);
        Assert.Contains("services.AddSingleton<WarningCenterView>()", app, StringComparison.Ordinal);
    }

    [Fact]
    public void FlattenedPreviewWarning_IsHiddenAndDiagnosticsRemainReadableAsCards()
    {
        var root = SolutionRootLocator.Find();
        var previewNavigation = Read(root, "src", "KKL.WordStudio.UI", "Views", "PreviewView.Diagnostics.cs");
        var warningCenter = Read(root, "src", "KKL.WordStudio.UI", "Views", "WarningCenterView.xaml");
        var renderer = Read(root, "src", "KKL.WordStudio.UI", "Preview", "PreviewRenderer.cs");

        Assert.Contains("SurfaceStatusText.StartsWith", previewNavigation, StringComparison.Ordinal);
        Assert.Contains("\"Önizleme uyarısı:\"", previewNavigation, StringComparison.Ordinal);
        Assert.Contains("Visibility.Collapsed", previewNavigation, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Items}\"", warningCenter, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Message}\"", warningCenter, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SourceText}\"", warningCenter, StringComparison.Ordinal);
        Assert.Contains("PreviewDiagnosticFactory.Build", renderer, StringComparison.Ordinal);
        Assert.Contains("_diagnosticsStore.Replace", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("Diagnostics =", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticClick_RoutesToPreviewElementAndExcelCellWithoutChangingLayoutOwnership()
    {
        var root = SolutionRootLocator.Find();
        var warningVm = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "WarningCenterViewModel.cs");
        var previewVm = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewViewModel.Diagnostics.cs");
        var previewView = Read(root, "src", "KKL.WordStudio.UI", "Views", "PreviewView.Diagnostics.cs");
        var excelVm = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "ExcelWorkspaceViewModel.Diagnostics.cs");
        var excelView = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.KeyboardFlow.cs");

        Assert.Contains("_previewViewModel.NavigateToElement", warningVm, StringComparison.Ordinal);
        Assert.Contains("NavigateToDiagnosticSourceAsync", warningVm, StringComparison.Ordinal);
        Assert.Contains("_workspace.SetSelectedReportElement", previewVm, StringComparison.Ordinal);
        Assert.Contains("BringIntoView", previewView, StringComparison.Ordinal);
        Assert.Contains("NavigateToWorksheetAsync", excelVm, StringComparison.Ordinal);
        Assert.Contains("FindDiagnosticMatches", excelVm, StringComparison.Ordinal);
        Assert.Contains("DiagnosticGridNavigationRequested", excelVm, StringComparison.Ordinal);
        Assert.Contains("WorkingDataGrid.CurrentCell = cell", excelView, StringComparison.Ordinal);
        Assert.Contains("WorkingDataGrid.ScrollIntoView", excelView, StringComparison.Ordinal);
        Assert.Contains("Keyboard.Focus(WorkingDataGrid)", excelView, StringComparison.Ordinal);

        Assert.DoesNotContain("IDocumentLayoutEngine", warningVm, StringComparison.Ordinal);
        Assert.DoesNotContain("IDocumentLayoutEngine", excelVm, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument", warningVm, StringComparison.Ordinal);
        Assert.DoesNotContain("WordprocessingDocument", excelVm, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticFactory_PreservesMessageAndAttachesKeyAndSourceMetadata()
    {
        var root = SolutionRootLocator.Find();
        var factory = Read(root, "src", "KKL.WordStudio.Application", "Preview", "PreviewDiagnosticFactory.cs");
        var contract = Read(root, "src", "KKL.WordStudio.Application", "Preview", "PreviewDiagnostic.cs");

        Assert.Contains("tableNode.CompositionWarnings", factory, StringComparison.Ordinal);
        Assert.Contains("Message = message", factory, StringComparison.Ordinal);
        Assert.Contains("PN/key", factory, StringComparison.Ordinal);
        Assert.Contains("table.Sources", factory, StringComparison.Ordinal);
        Assert.Contains("Workbook.SourcePath", factory, StringComparison.Ordinal);
        Assert.Contains("WorksheetName", factory, StringComparison.Ordinal);
        Assert.Contains("RangeReference", factory, StringComparison.Ordinal);
        Assert.Contains("KeyColumnIdentity", contract, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
