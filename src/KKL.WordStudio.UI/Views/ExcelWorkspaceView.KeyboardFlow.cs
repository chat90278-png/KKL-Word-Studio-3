namespace KKL.WordStudio.UI.Views;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using KKL.WordStudio.UI.ViewModels;

public partial class ExcelWorkspaceView
{
    private GridKeyboardAnchor? _lastGridKeyboardAnchor;
    private bool _restoreGridFocusAfterPreviewRefresh;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        WorkingDataGrid.CurrentCellChanged += WorkingDataGrid_CurrentCellChanged;
        WorkingDataGrid.AddHandler(
            Keyboard.PreviewKeyDownEvent,
            new KeyEventHandler(WorkingDataGrid_KeyboardFlowPreviewKeyDown),
            handledEventsToo: true);
        AddHandler(
            ButtonBase.ClickEvent,
            new RoutedEventHandler(WorkspaceButton_KeyboardFlowClick),
            handledEventsToo: true);
        _viewModel.PropertyChanged += ViewModel_KeyboardFlowPropertyChanged;
        Unloaded += ExcelWorkspaceView_KeyboardFlowUnloaded;
    }

    private void WorkingDataGrid_CurrentCellChanged(object? sender, EventArgs e)
    {
        var anchor = CaptureGridKeyboardAnchor();
        if (anchor is not null)
            _lastGridKeyboardAnchor = anchor;
    }

    private void WorkingDataGrid_KeyboardFlowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var refreshesGrid = e.Key == Key.Delete
            || control && e.Key is Key.V or Key.Z or Key.Y;
        if (!refreshesGrid)
            return;

        ArmGridKeyboardRestore();
        ScheduleGridKeyboardRestore(replaceSelection: true);
    }

    private void WorkspaceButton_KeyboardFlowClick(object sender, RoutedEventArgs e)
    {
        if (e.Source is not ContentControl { Content: string label })
            return;

        switch (label)
        {
            case "Kopyala":
                RememberCurrentGridAnchor();
                ScheduleGridKeyboardRestore(replaceSelection: false);
                break;

            case "Yapıştır":
            case "Temizle":
            case "Geri Al":
            case "Yinele":
            case "Kaynak Veriye Dön":
                ArmGridKeyboardRestore();
                // Restore once after the routed click completes, then once more
                // when an asynchronous PreviewTable rebuild arrives.
                ScheduleGridKeyboardRestore(replaceSelection: true);
                break;
        }
    }

    private void ViewModel_KeyboardFlowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ExcelWorkspaceViewModel.PreviewTable)
            || !_restoreGridFocusAfterPreviewRefresh)
        {
            return;
        }

        _restoreGridFocusAfterPreviewRefresh = false;
        ScheduleGridKeyboardRestore(replaceSelection: true);
    }

    private void ArmGridKeyboardRestore()
    {
        RememberCurrentGridAnchor();
        _restoreGridFocusAfterPreviewRefresh = true;
    }

    private void RememberCurrentGridAnchor()
    {
        var anchor = CaptureGridKeyboardAnchor();
        if (anchor is not null)
            _lastGridKeyboardAnchor = anchor;
    }

    private GridKeyboardAnchor? CaptureGridKeyboardAnchor()
    {
        var current = WorkingDataGrid.CurrentCell;
        if (!current.IsValid || current.Item is null || current.Column is null)
            return null;

        var rowIndex = WorkingDataGrid.Items.IndexOf(current.Item);
        var columnIdentity = GetColumnIdentity(current.Column);
        return rowIndex >= 0 && !string.IsNullOrWhiteSpace(columnIdentity)
            ? new GridKeyboardAnchor(rowIndex, columnIdentity)
            : null;
    }

    private void ScheduleGridKeyboardRestore(bool replaceSelection)
    {
        var anchor = _lastGridKeyboardAnchor;
        if (anchor is null)
            return;

        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => RestoreGridKeyboardFlow(anchor.Value, replaceSelection)));
    }

    private void RestoreGridKeyboardFlow(GridKeyboardAnchor anchor, bool replaceSelection)
    {
        if (!IsLoaded
            || !WorkingDataGrid.IsVisible
            || anchor.RowIndex < 0
            || anchor.RowIndex >= WorkingDataGrid.Items.Count)
        {
            return;
        }

        var column = WorkingDataGrid.Columns.FirstOrDefault(candidate =>
            string.Equals(
                GetColumnIdentity(candidate),
                anchor.ColumnIdentity,
                StringComparison.OrdinalIgnoreCase));
        if (column is null)
            return;

        var item = WorkingDataGrid.Items[anchor.RowIndex];
        var cell = new DataGridCellInfo(item, column);
        WorkingDataGrid.CurrentCell = cell;

        if (replaceSelection || WorkingDataGrid.SelectedCells.Count == 0)
        {
            WorkingDataGrid.UnselectAllCells();
            WorkingDataGrid.SelectedCells.Add(cell);
        }

        WorkingDataGrid.ScrollIntoView(item, column);
        WorkingDataGrid.Focus();
        Keyboard.Focus(WorkingDataGrid);
    }

    private void ExcelWorkspaceView_KeyboardFlowUnloaded(object sender, RoutedEventArgs e)
    {
        WorkingDataGrid.CurrentCellChanged -= WorkingDataGrid_CurrentCellChanged;
        _viewModel.PropertyChanged -= ViewModel_KeyboardFlowPropertyChanged;
        Unloaded -= ExcelWorkspaceView_KeyboardFlowUnloaded;
    }

    private readonly record struct GridKeyboardAnchor(int RowIndex, string ColumnIdentity);
}
