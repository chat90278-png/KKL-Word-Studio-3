namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using KKL.WordStudio.UI.ViewModels;

public partial class PreviewView
{
    private bool _diagnosticNavigationAttached;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += PreviewView_DiagnosticsLoaded;
        Unloaded += PreviewView_DiagnosticsUnloaded;
    }

    private void PreviewView_DiagnosticsLoaded(object sender, RoutedEventArgs e)
    {
        if (_diagnosticNavigationAttached)
            return;

        _viewModel.NavigateToElementRequested += ViewModel_NavigateToElementRequested;
        _diagnosticNavigationAttached = true;
    }

    private void PreviewView_DiagnosticsUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_diagnosticNavigationAttached)
            return;

        _viewModel.NavigateToElementRequested -= ViewModel_NavigateToElementRequested;
        _diagnosticNavigationAttached = false;
    }

    private void ViewModel_NavigateToElementRequested(Guid elementId)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => BringElementIntoView(elementId)));
    }

    private void BringElementIntoView(Guid elementId)
    {
        var page = _viewModel.Pages.FirstOrDefault(candidate =>
            candidate.Blocks.Any(block => block.ElementId == elementId));
        if (page is null)
            return;

        var pagesControl = FindVisualDescendants<ItemsControl>(this)
            .FirstOrDefault(control => ReferenceEquals(control.ItemsSource, _viewModel.Pages));
        if (pagesControl is null)
            return;

        pagesControl.UpdateLayout();
        if (pagesControl.ItemContainerGenerator.ContainerFromItem(page) is FrameworkElement pageContainer)
        {
            pageContainer.BringIntoView();
            Scroller.Focus();
        }
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualDescendants<T>(child))
                yield return descendant;
        }
    }
}
