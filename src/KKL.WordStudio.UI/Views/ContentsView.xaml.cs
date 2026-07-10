namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.UI.ViewModels;

public partial class ContentsView : UserControl
{
    private Point _dragStart;
    private ContentsNodeViewModel? _dragSource;

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

    // ---------------------------------------------------------------
    // Sprint 12: drag & drop gesture routing ONLY. This code-behind never
    // mutates report structure; it computes source Id, target Id and a
    // Before/Into/After drop mode from the pointer and calls the ViewModel,
    // which delegates to IReportStructureService. The DRAG IDENTITY is the real
    // ReportElement Guid — no ContentsNodeViewModel is dragged or persisted as
    // product state.
    // ---------------------------------------------------------------

    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragSource = (e.OriginalSource as FrameworkElement)?.DataContext as ContentsNodeViewModel;
    }

    private void Tree_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSource is null) return;
        var delta = e.GetPosition(null) - _dragStart;
        if (System.Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && System.Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // Carry only the real element Id as the drag payload.
        var payload = new DataObject(typeof(Guid), _dragSource.ElementId);
        DragDrop.DoDragDrop((DependencyObject)sender, payload, DragDropEffects.Move);
    }

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not ContentsViewModel viewModel) return;
        if (e.Data.GetData(typeof(Guid)) is not Guid sourceId) return;
        // The drop TARGET is resolved from the target row's DataContext (allowed).
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not ContentsNodeViewModel target) return;
        if (sourceId == target.ElementId) return;
        if (e.OriginalSource is not FrameworkElement targetElement) return;

        // Drop zone by vertical position within the target row: top→Before,
        // middle (heading only)→Into, bottom→After.
        var y = e.GetPosition(targetElement).Y;
        var height = targetElement.ActualHeight <= 0 ? 1 : targetElement.ActualHeight;
        var ratio = y / height;
        var mode = ratio < 0.25 ? StructureDropMode.Before
            : ratio > 0.75 ? StructureDropMode.After
            : target.Kind == ContentsNodeKind.Table ? StructureDropMode.After
            : StructureDropMode.Into;

        viewModel.MoveByDragDrop(sourceId, target.ElementId, mode);
        _dragSource = null;
    }

    /// <summary>
    /// Focus-loss commits the inline rename. The ViewModel's CommitRename is a
    /// no-op when the user already cancelled with Escape, so committing here is
    /// safe. Product logic (the actual rename) stays in the service.
    /// </summary>
    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ContentsViewModel viewModel && viewModel.IsRenaming)
            viewModel.CommitRenameCommand.Execute(null);
    }
}
