namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KKL.WordStudio.UI.ViewModels;

public partial class ExcelWorkspaceView
{
    private readonly List<GridSearchHit> _gridSearchHits = new();
    private int _gridSearchCursor = -1;
    private int _gridZoomPercent = 100;

    private void ExcelWorkspaceView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (control && e.Key == Key.F)
        {
            e.Handled = true;
            OpenGridSearch();
            return;
        }

        if (SearchStrip.Visibility != Visibility.Visible)
            return;

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseGridSearch();
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            MoveGridSearch(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
        }
    }

    private void ExcelWorkspaceView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        e.Handled = true;
        SetGridZoom(_gridZoomPercent + (e.Delta > 0 ? 10 : -10));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is ExcelWorkspaceViewModel)
            RebuildGridSearch();
    }

    private void SearchPrevious_Click(object sender, RoutedEventArgs e) => MoveGridSearch(-1);
    private void SearchNext_Click(object sender, RoutedEventArgs e) => MoveGridSearch(1);
    private void CloseSearch_Click(object sender, RoutedEventArgs e) => CloseGridSearch();
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetGridZoom(_gridZoomPercent - 10);
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetGridZoom(_gridZoomPercent + 10);
    private void ZoomReset_Click(object sender, RoutedEventArgs e) => SetGridZoom(100);

    private void OpenGridSearch()
    {
        SearchStrip.Visibility = Visibility.Visible;
        SearchBox.Focus();
        SearchBox.SelectAll();
        RebuildGridSearch();
    }

    private void CloseGridSearch()
    {
        SearchStrip.Visibility = Visibility.Collapsed;
        WorkingDataGrid.Focus();
    }

    private void RebuildGridSearch()
    {
        _gridSearchHits.Clear();
        _gridSearchCursor = -1;
        var query = SearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResultText.Text = "Aranacak metni yazın";
            return;
        }

        var table = _viewModel.PreviewTable;
        var identities = WorkingDataGrid.Columns
            .OrderBy(column => column.DisplayIndex)
            .Select(GetColumnIdentity)
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Cast<string>()
            .ToList();

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];
            foreach (var identity in identities)
            {
                if (!table.Columns.Contains(identity))
                    continue;
                var value = row[identity]?.ToString() ?? string.Empty;
                if (value.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                    _gridSearchHits.Add(new GridSearchHit(rowIndex, identity));
            }
        }

        if (_gridSearchHits.Count == 0)
        {
            SearchResultText.Text = "Sonuç yok";
            return;
        }

        _gridSearchCursor = 0;
        NavigateToGridSearchHit();
    }

    private void MoveGridSearch(int delta)
    {
        if (_gridSearchHits.Count == 0)
        {
            RebuildGridSearch();
            return;
        }

        _gridSearchCursor = (_gridSearchCursor + delta + _gridSearchHits.Count) % _gridSearchHits.Count;
        NavigateToGridSearchHit();
    }

    private void NavigateToGridSearchHit()
    {
        if (_gridSearchCursor < 0 || _gridSearchCursor >= _gridSearchHits.Count)
            return;

        var hit = _gridSearchHits[_gridSearchCursor];
        if (hit.RowIndex < 0 || hit.RowIndex >= WorkingDataGrid.Items.Count)
            return;

        var column = WorkingDataGrid.Columns.FirstOrDefault(candidate =>
            string.Equals(GetColumnIdentity(candidate), hit.ColumnIdentity, StringComparison.OrdinalIgnoreCase));
        if (column is null)
            return;

        var item = WorkingDataGrid.Items[hit.RowIndex];
        WorkingDataGrid.ScrollIntoView(item, column);
        var cell = new DataGridCellInfo(item, column);
        WorkingDataGrid.CurrentCell = cell;
        WorkingDataGrid.UnselectAllCells();
        WorkingDataGrid.SelectedCells.Add(cell);
        SearchResultText.Text = $"{_gridSearchCursor + 1} / {_gridSearchHits.Count}";
    }

    private void SetGridZoom(int percent)
    {
        _gridZoomPercent = Math.Clamp(percent, 50, 200);
        var scale = _gridZoomPercent / 100d;
        WorkingDataGrid.LayoutTransform = new ScaleTransform(scale, scale);
        ZoomPercentText.Text = $"{_gridZoomPercent}%";
    }

    private sealed record GridSearchHit(int RowIndex, string ColumnIdentity);
}
