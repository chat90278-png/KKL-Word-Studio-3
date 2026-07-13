namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.WorkingData;

public sealed partial class ExcelWorkspaceViewModel
{
    /// <summary>
    /// Closing the editor after a successful manual Apply is the point where all
    /// range fields are coherent. Persist them even when no WorkingData snapshot
    /// exists yet, so source/worksheet navigation cannot reset the user's choice.
    /// Cancelling a manual editor simply re-saves the unchanged active range;
    /// an unaccepted automatic candidate is deliberately ignored.
    /// </summary>
    partial void OnIsRangeEditorOpenChanged(bool value)
    {
        if (value
            || RangeIsAutomaticCandidate
            || WasAutoDetected
            || DetectedDataEndRow is not { } dataEndRow
            || dataEndRow < EffectiveDataStartRow)
        {
            return;
        }

        var project = _workspace.ActiveProject;
        if (project is null || SelectedWorkbook?.SelectedSheetName is not { } sheetName)
            return;

        _workingDataService.SaveSelectedRange(
            project,
            SelectedWorkbook.FilePath,
            SelectedWorkbook.DisplayName,
            sheetName,
            BuildCurrentRange(dataEndRow),
            string.IsNullOrWhiteSpace(DataSourceName) ? null : DataSourceName);

        SetWorkspaceDataSource(sheetName);
    }
}
