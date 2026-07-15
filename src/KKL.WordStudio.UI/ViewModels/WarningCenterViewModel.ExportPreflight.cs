namespace KKL.WordStudio.UI.ViewModels;

public sealed partial class WarningCenterViewModel
{
    public WarningCenterViewModel(
        PreviewDiagnosticsStore store,
        PreviewViewModel previewViewModel,
        ExcelWorkspaceViewModel excelWorkspaceViewModel,
        DockViewModel dockViewModel)
        : this(store, previewViewModel, excelWorkspaceViewModel)
    {
        dockViewModel.BlockingErrorsRequested += ShowErrorsForExportBlock;
    }

    /// <summary>
    /// Ensures an export-blocking Error cannot remain hidden behind a filter the
    /// user selected earlier. The existing Warning Center remains the only UI.
    /// </summary>
    private void ShowErrorsForExportBlock() => Filter = WarningCenterFilter.Error;
}
