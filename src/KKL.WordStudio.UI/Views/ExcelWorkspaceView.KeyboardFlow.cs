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
    private bool _keyboardFlowAttached;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        // InitializeComponent can raise Initialized before the constructor has
        // assigned the injected ViewModel. Defer every hook that touches
        // _viewModel or named XAML controls until Loaded.
        Loaded += ExcelWorkspaceView_KeyboardFlowLoaded;
        Unloaded += ExcelWorkspaceView_KeyboardFlowUnloaded;
    }

    private void ExcelWorkspaceView_KeyboardFlowLoaded(object sender, RoutedEventArgs e)
    {
        if (_keyboardFlowAttached)
            return;

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
        _viewModel.DiagnosticGridNavigationRequested += ViewModel_DiagnosticGridNavigationRequested;
        _keyboardFlowAttached = true;
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

    private void ViewModel_DiagnosticGridNavigationRequested(ExcelGridNavigationRequest request)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => NavigateToDiagnosticCell(request)));
    }

    private void NavigateToDiagnosticCell(ExcelGridNavigationRequest request)
    {
        if (!IsLoaded
            || !WorkingDataGrid.IsVisible
            || request.DisplayRowIndex < 0
            || request.DisplayRowIndex >= WorkingDataGrid.Items.Count)
        {
            return;
        }

        var column = !string.IsNullOrWhiteSpace(request.ColumnIdentity)
            ? WorkingDataGrid.Columns.FirstOrDefault(candidate => string.Equals(
                GetColumnIdentity(candidate),
                request.ColumnIdentity,
                StringComparison.OrdinalIgnoreCase))
            : null;
        column ??= WorkingDataGrid.Columns
            .OrderBy(candidate => candidate.DisplayIndex)
            .ElementAtOrDefault(request.ColumnIndex);
        if (column is null || !TryApplyGridCell(request.DisplayRowIndex, column, replaceSelection: true))
            return;

        var identity = GetColumnIdentity(column);
        if (!string.IsNullOrWhiteSpace(identity))
            _lastGridKeyboardAnchor = new GridKeyboardAnchor(request.DisplayRowIndex, identity);
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

        TryApplyGridCell(anchor.RowIndex, column, replaceSelection);
    }

    private bool TryApplyGridCell(int rowIndex, DataGridColumn column, bool replaceSelection)
    {
        if (!IsLoaded
            || !WorkingDataGrid.IsVisible
            || rowIndex < 0
            || rowIndex >= WorkingDataGrid.Items.Count
            || WorkingDataGrid.Columns.IndexOf(column) < 0)
        {
            return false;
        }

        try
        {
            var item = WorkingDataGrid.Items[rowIndex];
            var cell = new DataGridCellInfo(item, column);
            if (!cell.IsValid)
                return false;

            WorkingDataGrid.CurrentCell = cell;
            if (replaceSelection || WorkingDataGrid.SelectedCells.Count == 0)
            {
                WorkingDataGrid.UnselectAllCells();
                WorkingDataGrid.SelectedCells.Add(cell);
            }

            WorkingDataGrid.ScrollIntoView(item, column);
            WorkingDataGrid.Focus();
            Keyboard.Focus(WorkingDataGrid);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // PreviewTable may have rebuilt between a queued navigation request
            // and dispatcher execution. A stale anchor is ignored rather than
            // terminating the application through DispatcherUnhandledException.
            return false;
        }
        catch (InvalidOperationException)
        {
            // WPF can temporarily reject selection changes while an edit or
            // collection refresh is unwinding. The next user action will capture
            // a fresh anchor from the current grid projection.
            return false;
        }
    }

    private void ExcelWorkspaceView_KeyboardFlowUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_keyboardFlowAttached)
            return;

        WorkingDataGrid.CurrentCellChanged -= WorkingDataGrid_CurrentCellChanged;
        WorkingDataGrid.RemoveHandler(
            Keyboard.PreviewKeyDownEvent,
            new KeyEventHandler(WorkingDataGrid_KeyboardFlowPreviewKeyDown));
        RemoveHandler(
            ButtonBase.ClickEvent,
            new RoutedEventHandler(WorkspaceButton_KeyboardFlowClick));
        _viewModel.PropertyChanged -= ViewModel_KeyboardFlowPropertyChanged;
        _viewModel.DiagnosticGridNavigationRequested -= ViewModel_DiagnosticGridNavigationRequested;
        _keyboardFlowAttached = false;
    }

    private readonly record struct GridKeyboardAnchor(int RowIndex, string ColumnIdentity);
}