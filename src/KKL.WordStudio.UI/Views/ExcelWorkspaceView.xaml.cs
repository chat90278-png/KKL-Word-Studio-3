namespace KKL.WordStudio.UI.Views;

using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using KKL.WordStudio.Application.Importing;
using KKL.WordStudio.UI.ViewModels;

public partial class ExcelWorkspaceView : UserControl
{
    private readonly ExcelWorkspaceViewModel _viewModel;
    private GridInteractionContext _interactionContext = GridInteractionContext.Cell;
    private int _activeRowDisplayIndex = -1;
    private string? _activeColumnIdentity;
    private readonly HashSet<string> _selectedColumnHeaderIdentities = new(StringComparer.OrdinalIgnoreCase);

    public ExcelWorkspaceView(ExcelWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(ExcelWorkspaceViewModel.PreviewTable)) return;
            _interactionContext = GridInteractionContext.Cell;
            _activeRowDisplayIndex = -1;
            _activeColumnIdentity = null;
            _selectedColumnHeaderIdentities.Clear();
        };
    }

    private void DrawerScrim_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _viewModel.CancelMappingCommand.Execute(null);

    private void TransferChoiceScrim_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _viewModel.CancelTransferChoiceCommand.Execute(null);

    private void SourceFieldMappingScrim_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _viewModel.CancelSourceFieldMappingCommand.Execute(null);

    private void ExcelDropTarget_DragEnter(object sender, DragEventArgs e) => UpdateDropFeedback(e);
    private void ExcelDropTarget_DragOver(object sender, DragEventArgs e) => UpdateDropFeedback(e);

    private void ExcelDropTarget_DragLeave(object sender, DragEventArgs e) =>
        _viewModel.SetExcelDropActive(false);

    private async void ExcelDropTarget_Drop(object sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e);
        await _viewModel.HandleDroppedFilesAsync(files);
    }

    private void UpdateDropFeedback(DragEventArgs e)
    {
        var files = GetDroppedFiles(e);
        var decision = SourceFileDropValidator.EvaluateExcelDrop(files);
        e.Effects = decision.IsAccepted ? DragDropEffects.Copy : DragDropEffects.None;
        _viewModel.SetExcelDropActive(decision.IsAccepted);
        e.Handled = true;
    }

    private static IReadOnlyList<string> GetDroppedFiles(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files ? files : Array.Empty<string>();

    private void WorkingDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.PropertyName == "#")
        {
            // "#" is row-view metadata. The real row-header surface renders it;
            // it is never a user data column and cannot be hidden/deleted.
            e.Cancel = true;
            return;
        }

        var workingHeader = _viewModel.GetWorkingDataColumnHeader(e.PropertyName);
        if (!string.IsNullOrWhiteSpace(workingHeader))
            e.Column.Header = workingHeader;
    }

    private async void WorkingDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.EditingElement is not TextBox editor)
            return;

        var columnIdentity = GetColumnIdentity(e.Column);
        if (columnIdentity is null) return;

        e.Cancel = true;
        var rowIndex = WorkingDataGrid.Items.IndexOf(e.Row.Item);
        await _viewModel.CommitCellEditAsync(rowIndex, columnIdentity, editor.Text);
    }

    private async void ClearCells_Click(object sender, RoutedEventArgs e) =>
        await _viewModel.ClearCellsAsync(GetSelectedGridCells());

    private void Copy_Click(object sender, RoutedEventArgs e) => CopySelectionToClipboard();

    private async void Paste_Click(object sender, RoutedEventArgs e) => await PasteFromClipboardAsync();

    private void RowHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRowHeader header) return;
        ActivateRowHeader(header);
    }

    private void RowHeader_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRowHeader header) return;
        ActivateRowHeader(header);
        var targetRows = GetContextRowTargets(_activeRowDisplayIndex);
        header.ContextMenu = CreateRowContextMenu(targetRows);
        header.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridColumnHeader header) return;
        ActivateColumnHeader(header, isRightClick: false);
    }

    private void ColumnHeader_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridColumnHeader header) return;
        ActivateColumnHeader(header, isRightClick: true);
        if (_activeColumnIdentity is null) return;
        var targetColumns = GetContextColumnTargets(_activeColumnIdentity);
        header.ContextMenu = CreateColumnContextMenu(_activeColumnIdentity, targetColumns);
        header.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void Cell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _interactionContext = GridInteractionContext.Cell;
        if (sender is DataGridCell cell)
        {
            _activeRowDisplayIndex = WorkingDataGrid.Items.IndexOf(cell.DataContext);
            _activeColumnIdentity = GetColumnIdentity(cell.Column);
        }
    }

    private void Cell_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridCell cell) return;
        _interactionContext = GridInteractionContext.Cell;
        _activeRowDisplayIndex = WorkingDataGrid.Items.IndexOf(cell.DataContext);
        _activeColumnIdentity = GetColumnIdentity(cell.Column);
        if (_activeRowDisplayIndex < 0 || _activeColumnIdentity is null) return;

        var info = new DataGridCellInfo(cell.DataContext, cell.Column);
        WorkingDataGrid.CurrentCell = info;
        if (!WorkingDataGrid.SelectedCells.Contains(info))
        {
            WorkingDataGrid.UnselectAllCells();
            cell.IsSelected = true;
        }

        cell.ContextMenu = CreateCellContextMenu();
        cell.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void ActivateRowHeader(DataGridRowHeader header)
    {
        _interactionContext = GridInteractionContext.RowHeader;
        _activeRowDisplayIndex = WorkingDataGrid.Items.IndexOf(header.DataContext);
        _activeColumnIdentity = null;
    }

    private void ActivateColumnHeader(DataGridColumnHeader header, bool isRightClick)
    {
        _interactionContext = GridInteractionContext.ColumnHeader;
        _activeColumnIdentity = GetColumnIdentity(header.Column);
        _activeRowDisplayIndex = -1;
        if (_activeColumnIdentity is null) return;

        if (isRightClick)
        {
            if (!_selectedColumnHeaderIdentities.Contains(_activeColumnIdentity))
            {
                _selectedColumnHeaderIdentities.Clear();
                _selectedColumnHeaderIdentities.Add(_activeColumnIdentity);
            }
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (!_selectedColumnHeaderIdentities.Add(_activeColumnIdentity))
                _selectedColumnHeaderIdentities.Remove(_activeColumnIdentity);
            if (_selectedColumnHeaderIdentities.Count == 0)
                _selectedColumnHeaderIdentities.Add(_activeColumnIdentity);
        }
        else
        {
            _selectedColumnHeaderIdentities.Clear();
            _selectedColumnHeaderIdentities.Add(_activeColumnIdentity);
        }
    }

    private ContextMenu CreateRowContextMenu(IReadOnlyList<int> targetRows)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Üste Satır Ekle", async () => await _viewModel.InsertRowRelativeAsync(_activeRowDisplayIndex, below: false)));
        menu.Items.Add(CreateMenuItem("Alta Satır Ekle", async () => await _viewModel.InsertRowRelativeAsync(_activeRowDisplayIndex, below: true)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Satırı Sil", async () => await _viewModel.DeleteRowsAsync(targetRows)));
        menu.Items.Add(CreateMenuItem("Satırı Temizle", async () => await _viewModel.ClearRowsAsync(targetRows)));
        return menu;
    }

    private ContextMenu CreateColumnContextMenu(string activeIdentity, IReadOnlyList<string> targetColumns)
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Sola Sütun Ekle", async () => await _viewModel.InsertColumnRelativeAsync(activeIdentity, right: false)));
        menu.Items.Add(CreateMenuItem("Sağa Sütun Ekle", async () => await _viewModel.InsertColumnRelativeAsync(activeIdentity, right: true)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Sütunu Sil", async () => await _viewModel.DeleteColumnsByIdentityAsync(targetColumns)));
        menu.Items.Add(CreateMenuItem("Sütunu Gizle", async () => await _viewModel.HideColumnByIdentityAsync(activeIdentity)));
        menu.Items.Add(CreateMenuItem("Tüm Sütunları Göster", () =>
        {
            _viewModel.RestoreAllHiddenColumns();
            return Task.CompletedTask;
        }));
        return menu;
    }

    private ContextMenu CreateCellContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Kopyala", () =>
        {
            CopySelectionToClipboard();
            return Task.CompletedTask;
        }));
        menu.Items.Add(CreateMenuItem("Yapıştır", PasteFromClipboardAsync));
        menu.Items.Add(CreateMenuItem("Temizle", async () => await _viewModel.ClearCellsAsync(GetSelectedGridCells())));
        return menu;
    }

    private static MenuItem CreateMenuItem(string header, Func<Task> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += async (_, _) => await action();
        return item;
    }

    private async void WorkingDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (control && e.Key == Key.Z)
        {
            e.Handled = true;
            if (_viewModel.UndoCommand.CanExecute(null)) _viewModel.UndoCommand.Execute(null);
            return;
        }

        if (control && e.Key == Key.Y)
        {
            e.Handled = true;
            if (_viewModel.RedoCommand.CanExecute(null)) _viewModel.RedoCommand.Execute(null);
            return;
        }

        if (control && IsPlusKey(e.Key))
        {
            e.Handled = true;
            await InsertForActiveHeaderAsync();
            return;
        }

        if (control && IsMinusKey(e.Key))
        {
            e.Handled = true;
            await DeleteForActiveHeaderAsync();
            return;
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await _viewModel.ClearCellsAsync(GetSelectedGridCells());
            return;
        }

        if (control && e.Key == Key.C)
        {
            e.Handled = true;
            CopySelectionToClipboard();
            return;
        }

        if (control && e.Key == Key.V)
        {
            e.Handled = true;
            await PasteFromClipboardAsync();
        }
    }

    private async Task InsertForActiveHeaderAsync()
    {
        switch (_interactionContext)
        {
            case GridInteractionContext.RowHeader when _activeRowDisplayIndex >= 0:
                await _viewModel.InsertRowRelativeAsync(_activeRowDisplayIndex, below: false);
                break;
            case GridInteractionContext.ColumnHeader when _activeColumnIdentity is not null:
                await _viewModel.InsertColumnRelativeAsync(_activeColumnIdentity, right: false);
                break;
            default:
                _viewModel.ShowHeaderSelectionHint();
                break;
        }
    }

    private async Task DeleteForActiveHeaderAsync()
    {
        switch (_interactionContext)
        {
            case GridInteractionContext.RowHeader when _activeRowDisplayIndex >= 0:
                await _viewModel.DeleteRowsAsync(GetContextRowTargets(_activeRowDisplayIndex));
                break;
            case GridInteractionContext.ColumnHeader when _activeColumnIdentity is not null:
                await _viewModel.DeleteColumnsByIdentityAsync(GetContextColumnTargets(_activeColumnIdentity));
                break;
            default:
                _viewModel.ShowHeaderSelectionHint();
                break;
        }
    }

    private static bool IsPlusKey(Key key) => key is Key.OemPlus or Key.Add;
    private static bool IsMinusKey(Key key) => key is Key.OemMinus or Key.Subtract;

    private IReadOnlyList<GridCellTarget> GetSelectedGridCells() =>
        WorkingDataGrid.SelectedCells
            .Select(cell => new GridCellTarget(
                WorkingDataGrid.Items.IndexOf(cell.Item),
                GetColumnIdentity(cell.Column) ?? string.Empty))
            .Where(cell => cell.DisplayRowIndex >= 0 && !string.IsNullOrWhiteSpace(cell.ColumnIdentity))
            .Distinct()
            .ToList();

    private IReadOnlyList<int> GetSelectedRowIndexes() =>
        WorkingDataGrid.SelectedCells
            .Select(cell => WorkingDataGrid.Items.IndexOf(cell.Item))
            .Where(index => index >= 0)
            .Distinct()
            .ToList();

    private IReadOnlyList<int> GetContextRowTargets(int clickedRow)
    {
        var selected = GetSelectedRowIndexes().ToList();
        return selected.Contains(clickedRow) ? selected : new[] { clickedRow };
    }

    private IReadOnlyList<string> GetSelectedColumnIdentities() =>
        WorkingDataGrid.SelectedCells
            .Select(cell => GetColumnIdentity(cell.Column))
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Select(identity => identity!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private IReadOnlyList<string> GetContextColumnTargets(string clickedIdentity)
    {
        if (_selectedColumnHeaderIdentities.Contains(clickedIdentity))
            return _selectedColumnHeaderIdentities.ToList();

        var selectedCells = GetSelectedColumnIdentities().ToList();
        return selectedCells.Contains(clickedIdentity, StringComparer.OrdinalIgnoreCase) ? selectedCells : new[] { clickedIdentity };
    }

    private int GetCurrentRowIndex() => WorkingDataGrid.CurrentItem is null
        ? -1
        : WorkingDataGrid.Items.IndexOf(WorkingDataGrid.CurrentItem);

    private string? GetCurrentColumnIdentity() => GetColumnIdentity(WorkingDataGrid.CurrentColumn);

    private static string? GetColumnIdentity(DataGridColumn? column) =>
        column is DataGridBoundColumn boundColumn && boundColumn.Binding is Binding binding
            ? binding.Path?.Path
            : null;

    private async Task PasteFromClipboardAsync()
    {
        if (!Clipboard.ContainsText()) return;
        var rowIndex = GetCurrentRowIndex();
        var columnIdentity = GetCurrentColumnIdentity();
        if (rowIndex < 0 || columnIdentity is null) return;
        await _viewModel.PasteClipboardAsync(rowIndex, columnIdentity, Clipboard.GetText(TextDataFormat.Text));
    }

    private void CopySelectionToClipboard()
    {
        var cells = WorkingDataGrid.SelectedCells
            .Select(cell => new
            {
                RowIndex = WorkingDataGrid.Items.IndexOf(cell.Item),
                ColumnIndex = cell.Column.DisplayIndex,
                Value = ReadCellText(cell)
            })
            .Where(cell => cell.RowIndex >= 0 && cell.ColumnIndex >= 0)
            .ToList();
        if (cells.Count == 0) return;

        var minRow = cells.Min(cell => cell.RowIndex);
        var maxRow = cells.Max(cell => cell.RowIndex);
        var minColumn = cells.Min(cell => cell.ColumnIndex);
        var maxColumn = cells.Max(cell => cell.ColumnIndex);
        var lookup = cells.ToDictionary(cell => (cell.RowIndex, cell.ColumnIndex), cell => cell.Value);
        var lines = new List<string>();

        for (var rowIndex = minRow; rowIndex <= maxRow; rowIndex++)
        {
            var values = new List<string>();
            for (var columnIndex = minColumn; columnIndex <= maxColumn; columnIndex++)
                values.Add(lookup.TryGetValue((rowIndex, columnIndex), out var value) ? value : string.Empty);
            lines.Add(string.Join('\t', values));
        }

        Clipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    private static string ReadCellText(DataGridCellInfo cell)
    {
        if (cell.Item is not DataRowView rowView || cell.Column is not DataGridBoundColumn boundColumn || boundColumn.Binding is not Binding binding)
            return string.Empty;
        var columnName = binding.Path?.Path;
        return !string.IsNullOrWhiteSpace(columnName) && rowView.Row.Table.Columns.Contains(columnName)
            ? rowView.Row[columnName]?.ToString() ?? string.Empty
            : string.Empty;
    }

    private enum GridInteractionContext
    {
        Cell,
        RowHeader,
        ColumnHeader
    }
}
