namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.UI.ViewModels;

public partial class ContentsView : UserControl
{
    private Point _dragStart;
    private ContentsNodeViewModel? _dragSource;
    private TreeViewItem? _dropTargetItem;

    public ContentsView(ContentsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Tree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ContentsNodeViewModel node)
            node.OnSelected?.Invoke();
    }

    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragSource = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext
            as ContentsNodeViewModel;
    }

    private void Tree_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSource is null) return;
        var delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var payload = new DataObject(typeof(Guid), _dragSource.ElementId);
        DragDrop.DoDragDrop((DependencyObject)sender, payload, DragDropEffects.Move);
        _dragSource = null;
        ClearDropIndicator();
    }

    private void Tree_DragOver(object sender, DragEventArgs e)
    {
        if (!TryResolveDropTarget(e, out var item, out _, out var mode))
        {
            e.Effects = DragDropEffects.None;
            ClearDropIndicator();
            e.Handled = true;
            return;
        }

        ShowDropIndicator(item, mode);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void Tree_DragLeave(object sender, DragEventArgs e)
    {
        if (!Tree.IsMouseOver)
            ClearDropIndicator();
    }

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not ContentsViewModel viewModel) return;
            if (e.Data.GetData(typeof(Guid)) is not Guid sourceId) return;
            if (!TryResolveDropTarget(e, out _, out var target, out var mode)) return;
            if (sourceId == target.ElementId) return;

            viewModel.MoveByDragDropV23(sourceId, target.ElementId, mode);
            e.Handled = true;
        }
        finally
        {
            _dragSource = null;
            ClearDropIndicator();
        }
    }

    private static StructureDropMode ResolveDropMode(
        DragEventArgs e,
        TreeViewItem targetItem,
        ContentsNodeViewModel target)
    {
        var y = e.GetPosition(targetItem).Y;
        var height = targetItem.ActualHeight <= 0 ? 1 : targetItem.ActualHeight;
        var ratio = y / height;
        if (ratio < 0.25) return StructureDropMode.Before;
        if (ratio > 0.75) return StructureDropMode.After;
        return target.Kind == ContentsNodeKind.Table
            ? StructureDropMode.After
            : StructureDropMode.Into;
    }

    private static bool TryResolveDropTarget(
        DragEventArgs e,
        out TreeViewItem item,
        out ContentsNodeViewModel target,
        out StructureDropMode mode)
    {
        item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject)!;
        target = item?.DataContext as ContentsNodeViewModel ?? null!;
        if (item is null || target is null || !e.Data.GetDataPresent(typeof(Guid)))
        {
            mode = default;
            return false;
        }

        mode = ResolveDropMode(e, item, target);
        return true;
    }

    private void ShowDropIndicator(TreeViewItem item, StructureDropMode mode)
    {
        if (!ReferenceEquals(_dropTargetItem, item))
            ClearDropIndicator();

        _dropTargetItem = item;
        item.BorderBrush = Brushes.DodgerBlue;
        item.BorderThickness = mode switch
        {
            StructureDropMode.Before => new Thickness(0, 2, 0, 0),
            StructureDropMode.After => new Thickness(0, 0, 0, 2),
            _ => new Thickness(2)
        };
        item.Background = mode == StructureDropMode.Into
            ? new SolidColorBrush(Color.FromArgb(30, 30, 120, 255))
            : Brushes.Transparent;
        item.ToolTip = mode switch
        {
            StructureDropMode.Before => "Önüne taşı",
            StructureDropMode.After => "Sonrasına taşı",
            _ => "İçine taşı"
        };
    }

    private void ClearDropIndicator()
    {
        if (_dropTargetItem is null) return;
        _dropTargetItem.ClearValue(Border.BorderBrushProperty);
        _dropTargetItem.ClearValue(Border.BorderThicknessProperty);
        _dropTargetItem.ClearValue(Control.BackgroundProperty);
        _dropTargetItem.ClearValue(ToolTipProperty);
        _dropTargetItem = null;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ContentsViewModel viewModel && viewModel.IsRenaming)
            viewModel.CommitStructuredRenameCommand.Execute(null);
    }
}
