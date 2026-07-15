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
        Assert.Contains("Text=\"Uyarılar\"", dock, StringComparison.Ordinal);
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
        Assert.Contains("açık bulgu sayısı otomatik yenilenir", warningCenter, StringComparison.Ordinal);
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
        var excelEdit = Read(root, "src", "KKL.WordStudio.UI", "Views", "ExcelWorkspaceView.Sprint23.cs");
        var store = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewDiagnosticsStore.cs");

        Assert.Contains("_previewViewModel.NavigateToElement", warningVm, StringComparison.Ordinal);
        Assert.Contains("group.KeyValues", warningVm, StringComparison.Ordinal);
        Assert.Contains("group.AffectedColumn", warningVm, StringComparison.Ordinal);
        Assert.Contains("_workspace.SetSelectedReportElement", previewVm, StringComparison.Ordinal);
        Assert.Contains("BringIntoView", previewView, StringComparison.Ordinal);
        Assert.Contains("NavigateToWorksheetAsync", excelVm, StringComparison.Ordinal);
        Assert.Contains("ResolveDiagnosticColumnIndex(worksheet, affectedColumn)", excelVm, StringComparison.Ordinal);
        Assert.Contains("targetColumnIndex = affectedColumnIndex", excelVm, StringComparison.Ordinal);
        Assert.Contains("ReadDiagnosticCellText(worksheet, match)?.Trim()", excelVm, StringComparison.Ordinal);
        Assert.Contains("preferredMatches.Count == 0", excelVm, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstOrDefault(candidate =>", excelVm, StringComparison.Ordinal);
        Assert.DoesNotContain("exactMatches.Count > 0 ? exactMatches : allMatches", excelVm, StringComparison.Ordinal);
        Assert.Contains("var previewColumnIndex = columnIndex + 1", excelVm, StringComparison.Ordinal);
        Assert.Contains("DiagnosticGridNavigationRequested", excelVm, StringComparison.Ordinal);
        Assert.Contains("TryApplyGridCell", excelView, StringComparison.Ordinal);
        Assert.Contains("catch (ArgumentOutOfRangeException)", excelView, StringComparison.Ordinal);
        Assert.Contains("WorkingDataGrid.CurrentCell = cell", excelView, StringComparison.Ordinal);
        Assert.Contains("WorkingDataGrid.ScrollIntoView", excelView, StringComparison.Ordinal);
        Assert.Contains("Keyboard.Focus(WorkingDataGrid)", excelView, StringComparison.Ordinal);
        Assert.Contains("DispatcherPriority.Background", excelEdit, StringComparison.Ordinal);
        Assert.Contains("CommitCellEditAfterGridSettlesAsync", excelEdit, StringComparison.Ordinal);
        Assert.Contains("WorkingDataGrid.Items.IndexOf(rowItem)", excelEdit, StringComparison.Ordinal);
        Assert.Contains("TryCancelPendingGridEdit", excelEdit, StringComparison.Ordinal);
        Assert.Contains("CancelEdit(editingUnit)", excelEdit, StringComparison.Ordinal);
        Assert.Contains("public int FindingCount", store, StringComparison.Ordinal);

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
        var classifier = Read(root, "src", "KKL.WordStudio.Application", "Tables", "TableCompositionDiagnostic.cs");

        Assert.Contains("tableNode.CompositionDiagnostics", factory, StringComparison.Ordinal);
        Assert.Contains("Code = finding.Code", factory, StringComparison.Ordinal);
        Assert.Contains("GroupingKey = BuildGroupingKey", factory, StringComparison.Ordinal);
        Assert.Contains("Message = finding.Message", factory, StringComparison.Ordinal);
        Assert.Contains("table.Sources", factory, StringComparison.Ordinal);
        Assert.Contains("Workbook.SourcePath", factory, StringComparison.Ordinal);
        Assert.Contains("WorksheetName", factory, StringComparison.Ordinal);
        Assert.Contains("RangeReference", factory, StringComparison.Ordinal);
        Assert.Contains("PN/key", classifier, StringComparison.Ordinal);
        Assert.Contains("public string Code", contract, StringComparison.Ordinal);
        Assert.Contains("public string GroupingKey", contract, StringComparison.Ordinal);
        Assert.Contains("KeyColumnIdentity", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveTitle", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("TryExtractKey", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratedRegex", factory, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
