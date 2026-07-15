namespace KKL.WordStudio.UI.ViewModels;

public sealed partial class WarningCenterViewModel
{
    /// <summary>
    /// Ensures an export-blocking Error cannot remain hidden behind a filter the
    /// user selected earlier. The existing Warning Center remains the only UI.
    /// </summary>
    public void ShowErrorsForExportBlock() => Filter = WarningCenterFilter.Error;
}
