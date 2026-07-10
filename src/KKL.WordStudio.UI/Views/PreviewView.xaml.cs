namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Importing;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.UI.ViewModels;

public partial class PreviewView : UserControl
{
    private const string ReportElementDataFormat = "KKL.WordStudio.ReportElementId";

    private readonly PreviewViewModel _viewModel;
    private Point _dragStartPoint;
    private Guid? _dragSourceElementId;

    public PreviewView(PreviewViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.ViewportWidth = e.NewSize.Width;
        _viewModel.ViewportHeight = e.NewSize.Height;
    }

    // ---------------------------------------------------------------
    // Positioned report-block selection / inline editing / drag source
    // ---------------------------------------------------------------

    private void PageBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PreviewPageBlockViewModel block } host)
            return;
        if (FindAncestor<TextBox>(e.OriginalSource as DependencyObject) is not null)
            return;

        host.Focus();
        if (!block.CanInteract || block.ElementId is not { } elementId)
            return;

        if (e.ClickCount == 2 && block is PreviewTextPageBlockViewModel textBlock)
        {
            _dragSourceElementId = null;
            _viewModel.BeginTextEdit(textBlock);
            e.Handled = true;
            return;
        }

        _viewModel.SelectBlock(block);
        _dragStartPoint = e.GetPosition(this);
        _dragSourceElementId = block.CanStructureInteract ? elementId : null;
        e.Handled = true;
    }

    private void PageBlock_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragSourceElementId is not { } sourceElementId)
            return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragSourceElementId = null;
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var data = new DataObject(ReportElementDataFormat, sourceElementId.ToString("D"));
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            _dragSourceElementId = null;
            _viewModel.ClearDropIndicator();
        }
    }

    private void PageBlock_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PreviewPageBlockViewModel block } host || !block.CanStructureInteract)
            return;

        _viewModel.SelectBlock(block);
        var deleteItem = new MenuItem { Header = "Sil" };
        deleteItem.Click += (_, _) => _viewModel.DeletePreviewElement(block);

        var menu = new ContextMenu { PlacementTarget = host };
        menu.Items.Add(deleteItem);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void PreviewView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox)
        {
            _viewModel.DeleteSelectedPreviewElement();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.ClearDropIndicator();
        }
    }

    // ---------------------------------------------------------------
    // Report-element drop target. Gesture math is UI-only; product movement
    // is delegated to IReportStructureService by PreviewViewModel.
    // ---------------------------------------------------------------

    private void PageBlock_DragOver(object sender, DragEventArgs e)
    {
        if (!TryResolveReportDrop(sender, e, out var block, out var sourceElementId, out var targetElementId, out var mode))
        {
            _viewModel.ClearDropIndicator();
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        _viewModel.SetDropIndicator(block, mode);
        e.Effects = sourceElementId == targetElementId ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void PageBlock_DragLeave(object sender, DragEventArgs e) => _viewModel.ClearDropIndicator();

    private void PageBlock_Drop(object sender, DragEventArgs e)
    {
        if (TryResolveReportDrop(sender, e, out _, out var sourceElementId, out var targetElementId, out var mode)
            && sourceElementId != targetElementId)
        {
            _viewModel.MoveByDragDrop(sourceElementId, targetElementId, mode);
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        _viewModel.ClearDropIndicator();
        e.Handled = true;
    }

    private static bool TryResolveReportDrop(
        object sender,
        DragEventArgs e,
        out PreviewPageBlockViewModel block,
        out Guid sourceElementId,
        out Guid targetElementId,
        out StructureDropMode mode)
    {
        block = null!;
        sourceElementId = Guid.Empty;
        targetElementId = Guid.Empty;
        mode = StructureDropMode.Before;

        if (sender is not FrameworkElement { DataContext: PreviewPageBlockViewModel targetBlock } targetElement
            || !targetBlock.CanStructureInteract
            || targetBlock.ElementId is not { } resolvedTargetId
            || !TryGetDraggedElementId(e.Data, out var resolvedSourceId))
            return false;

        var pointerY = e.GetPosition(targetElement).Y;
        var targetHeight = targetElement.ActualHeight > 0 ? targetElement.ActualHeight : targetBlock.Height;
        var targetIsHeading = targetBlock is PreviewTextPageBlockViewModel
        {
            SemanticKind: ReportContentKind.Heading or ReportContentKind.AltHeading
        };

        block = targetBlock;
        sourceElementId = resolvedSourceId;
        targetElementId = resolvedTargetId;
        mode = PreviewInteractionHelpers.ResolveDropMode(pointerY, targetHeight, targetIsHeading);
        return true;
    }

    private static bool TryGetDraggedElementId(IDataObject data, out Guid elementId)
    {
        elementId = Guid.Empty;
        return data.GetDataPresent(ReportElementDataFormat)
               && data.GetData(ReportElementDataFormat) is string text
               && Guid.TryParse(text, out elementId);
    }

    // ---------------------------------------------------------------
    // Existing semantic inline editors
    // ---------------------------------------------------------------

    private void TableCaption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2
            || sender is not FrameworkElement { DataContext: PreviewTablePageBlockViewModel block }
            || !block.CanEditCaption)
            return;

        _viewModel.BeginTableCaptionEdit(block);
        e.Handled = true;
    }

    private void TableHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2
            || sender is not FrameworkElement { DataContext: PreviewTablePageColumnViewModel column }
            || !column.IsEditable)
            return;

        _viewModel.BeginTableHeaderEdit(column);
        e.Handled = true;
    }

    private void Editor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is TextBox editor)
        {
            editor.Dispatcher.BeginInvoke(() =>
            {
                editor.Focus();
                editor.SelectAll();
            });
        }
    }

    private void TextEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: PreviewTextPageBlockViewModel block })
            return;

        if (e.Key == Key.Enter)
        {
            _viewModel.CommitTextEdit(block);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CancelTextEdit(block);
            e.Handled = true;
        }
    }

    private void TextEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: PreviewTextPageBlockViewModel block })
            _viewModel.CommitTextEdit(block);
    }

    private void TableCaptionEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: PreviewTablePageBlockViewModel block })
            return;

        if (e.Key == Key.Enter)
        {
            _viewModel.CommitTableCaptionEdit(block);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CancelTableCaptionEdit(block);
            e.Handled = true;
        }
    }

    private void TableCaptionEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: PreviewTablePageBlockViewModel block })
            _viewModel.CommitTableCaptionEdit(block);
    }

    private void HeaderEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: PreviewTablePageColumnViewModel column })
            return;

        if (e.Key == Key.Enter)
        {
            _viewModel.CommitTableHeaderEdit(column);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel.CancelTableHeaderEdit(column);
            e.Handled = true;
        }
    }

    private void HeaderEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: PreviewTablePageColumnViewModel column })
            _viewModel.CommitTableHeaderEdit(column);
    }

    // ---------------------------------------------------------------
    // DOCX front matter file drop remains a surface-level file gesture.
    // Internal report-element drags are handled by page blocks above.
    // ---------------------------------------------------------------

    private void ReportDesigner_DragEnter(object sender, DragEventArgs e) => UpdateFrontMatterDropFeedback(e);
    private void ReportDesigner_DragOver(object sender, DragEventArgs e) => UpdateFrontMatterDropFeedback(e);

    private void ReportDesigner_DragLeave(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ReportElementDataFormat))
            _viewModel.SetFrontMatterDropActive(false);
    }

    private void ReportDesigner_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ReportElementDataFormat))
            return;

        _viewModel.HandleDroppedFrontMatterFiles(GetDroppedFiles(e));
    }

    private void UpdateFrontMatterDropFeedback(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ReportElementDataFormat))
            return;

        var decision = SourceFileDropValidator.EvaluateFrontMatterDrop(GetDroppedFiles(e));
        e.Effects = decision.IsAccepted ? DragDropEffects.Copy : DragDropEffects.None;
        _viewModel.SetFrontMatterDropActive(decision.IsAccepted);
        e.Handled = true;
    }

    private static IReadOnlyList<string> GetDroppedFiles(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files ? files : Array.Empty<string>();

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = current switch
            {
                ContentElement contentElement => ContentOperations.GetParent(contentElement)
                    ?? (contentElement as FrameworkContentElement)?.Parent,
                Visual or Visual3D => VisualTreeHelper.GetParent(current),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return null;
    }
}
