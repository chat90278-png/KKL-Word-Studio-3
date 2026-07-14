namespace KKL.WordStudio.Architecture.Tests;

using Xunit;

public sealed class Sprint24WarningCenterArchitectureTests
{
    [Fact]
    public void WarningCenter_UsesApplicationGroupingInsteadOfHidingRawDiagnostics()
    {
        var root = SolutionRootLocator.Find();
        var summary = Read(root, "src", "KKL.WordStudio.Application", "Preview", "PreviewDiagnosticSummaryService.cs");
        var store = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewDiagnosticsStore.cs");

        Assert.Contains("GroupBy(CreateKey)", summary, StringComparison.Ordinal);
        Assert.Contains("OccurrenceCount = occurrenceCount", summary, StringComparison.Ordinal);
        Assert.Contains("public int RawCount", store, StringComparison.Ordinal);
        Assert.Contains("PreviewDiagnosticSummaryService.Group(Items)", store, StringComparison.Ordinal);
        Assert.DoesNotContain("Items.Remove", store, StringComparison.Ordinal);
    }

    [Fact]
    public void WarningCenter_ExposesSeverityFiltersAndStableNavigation()
    {
        var root = SolutionRootLocator.Find();
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "WarningCenterView.xaml");
        var viewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "WarningCenterViewModel.cs");

        Assert.Contains("ShowErrorsCommand", view, StringComparison.Ordinal);
        Assert.Contains("ShowWarningsCommand", view, StringComparison.Ordinal);
        Assert.Contains("ShowInformationCommand", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Tümü\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Hata\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Uyarı\"", view, StringComparison.Ordinal);
        Assert.Contains("Text=\"Bilgi\"", view, StringComparison.Ordinal);
        Assert.Contains("OccurrenceText", view, StringComparison.Ordinal);
        Assert.Contains("ReportPaneViewModel.Shared.OpenForAction()", viewModel, StringComparison.Ordinal);
        Assert.Contains("_previewViewModel.NavigateToElement(elementId)", viewModel, StringComparison.Ordinal);
        Assert.Contains("NavigateToDiagnosticSourceAsync", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void ContextDockBadge_UsesActionableGroupCountAndBlockingSeverityColor()
    {
        var root = SolutionRootLocator.Find();
        var dock = Read(root, "src", "KKL.WordStudio.UI", "Views", "ContextDockView.xaml");
        var store = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "PreviewDiagnosticsStore.cs");

        Assert.Contains("Text=\"Kontrol\"", dock, StringComparison.Ordinal);
        Assert.Contains("Diagnostics.CountText", dock, StringComparison.Ordinal);
        Assert.Contains("Diagnostics.BadgeBackground", dock, StringComparison.Ordinal);
        Assert.Contains("HasBlockingErrors", store, StringComparison.Ordinal);
        Assert.Contains("Count > 99 ? \"99+\"", store, StringComparison.Ordinal);
    }

    [Fact]
    public void ControlCenter_UsesStableCodesReadableCardsAndOccurrenceNavigation()
    {
        var root = SolutionRootLocator.Find();
        var catalog = Read(root, "src", "KKL.WordStudio.Application", "Preview", "PreviewDiagnosticCatalog.cs");
        var summary = Read(root, "src", "KKL.WordStudio.Application", "Preview", "PreviewDiagnosticSummaryService.cs");
        var view = Read(root, "src", "KKL.WordStudio.UI", "Views", "WarningCenterView.xaml");
        var viewModel = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "WarningCenterViewModel.cs");
        var occurrenceDetails = Read(root, "src", "KKL.WordStudio.UI", "ViewModels", "WarningCenterViewModel.OccurrenceDetails.cs");

        Assert.Contains("QUANTITY_INVALID", catalog, StringComparison.Ordinal);
        Assert.Contains("SRC_FILE_MISSING", catalog, StringComparison.Ordinal);
        Assert.Contains("TABLE_SPLIT", catalog, StringComparison.Ordinal);
        Assert.Contains("ROWS_SKIPPED", catalog, StringComparison.Ordinal);
        Assert.Contains("DEFAULT_TITLE_USED", catalog, StringComparison.Ordinal);
        Assert.Contains("first.Code", summary, StringComparison.Ordinal);
        Assert.Contains("RowNumbers", summary, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding PrimaryNavigationText}\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Sonraki\"", view, StringComparison.Ordinal);
        Assert.Contains("Content=\"Tümünü Göster\"", view, StringComparison.Ordinal);
        Assert.Contains("NavigateFirstCommand", view, StringComparison.Ordinal);
        Assert.Contains("NavigateNextCommand", view, StringComparison.Ordinal);
        Assert.Contains("ShowAllOccurrencesCommand", view, StringComparison.Ordinal);
        Assert.Contains("PrimaryNavigationText", viewModel, StringComparison.Ordinal);
        Assert.Contains("CanNavigateNext => NavigationTargetCount > 1", viewModel, StringComparison.Ordinal);
        Assert.Contains("GetNavigationKey", viewModel, StringComparison.Ordinal);
        Assert.Contains("NavigationStatusText = string.Join", occurrenceDetails, StringComparison.Ordinal);
        Assert.Contains("Word'e hazır", viewModel, StringComparison.Ordinal);
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));
}
