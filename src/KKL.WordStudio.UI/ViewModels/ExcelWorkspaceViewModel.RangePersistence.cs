namespace KKL.WordStudio.UI.ViewModels;

using KKL.WordStudio.Application.WorkingData;
using KKL.WordStudio.Domain.DataSources;

public sealed partial class ExcelWorkspaceViewModel
{
    private DataRange? _rangeBeforeEditor;

    /// <summary>
    /// Captures the active range when the editor opens. Closing after Apply
    /// persists the coherent manual values without forcing WorkingData creation.
    /// Closing after an unaccepted Redetect restores the previous range instead
    /// of letting the temporary automatic candidate replace what the user saved.
    /// </summary>
    partial void OnIsRangeEditorOpenChanged(bool value)
    {
        if (value)
        {
            _rangeBeforeEditor = DetectedDataEndRow is { } currentEnd
                ? CloneRange(BuildCurrentRange(currentEnd))
                : null;
            return;
        }

        var previous = _rangeBeforeEditor;
        _rangeBeforeEditor = null;

        if (RangeIsAutomaticCandidate && previous is not null)
        {
            RestoreRange(previous);
            return;
        }

        if (RangeIsAutomaticCandidate
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

    private void RestoreRange(DataRange range)
    {
        StartRowIsHeader = range.HeaderRowIndex.HasValue;
        StartRow = range.HeaderRowIndex ?? range.DataStartRow;
        ConfiguredDataStartRow = range.DataStartRow;
        ConfiguredStartColumn = range.StartColumn ?? 1;
        ConfiguredEndColumn = range.EndColumn ?? Math.Max(ConfiguredStartColumn, _currentPreview?.ColumnCount ?? ConfiguredStartColumn);
        DetectedDataEndRow = range.DataEndRow;
        WasAutoDetected = range.WasAutoDetected;
        RangeIsAutomaticCandidate = false;
        RangeRequiresReview = false;
        TransferToReportCommand.NotifyCanExecuteChanged();
    }
}
