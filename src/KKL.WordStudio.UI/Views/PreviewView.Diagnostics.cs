namespace KKL.WordStudio.UI.Views;

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using KKL.WordStudio.UI.ViewModels;

public partial class PreviewView
{
    private bool _diagnosticNavigationAttached;
    private TextBlock? _surfaceStatusTextBlock;

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
        _viewModel.PropertyChanged += ViewModel_DiagnosticsPropertyChanged;
        _surfaceStatusTextBlock = FindVisualDescendants<TextBlock>(this)
            .FirstOrDefault(textBlock => string.Equals(
                BindingOperations.GetBinding(textBlock, TextBlock.TextProperty)?.Path?.Path,
                nameof(PreviewViewModel.SurfaceStatusText),
                StringComparison.Ordinal));
        UpdateSurfaceStatusVisibility();
        _diagnosticNavigationAttached = true;
    }

    private void PreviewView_DiagnosticsUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_diagnosticNavigationAttached)
            return;

        _viewModel.NavigateToElementRequested -= ViewModel_NavigateToElementRequested;
        _viewModel.PropertyChanged -= ViewModel_DiagnosticsPropertyChanged;
        _surfaceStatusTextBlock = null;
        _diagnosticNavigationAttached = false;
    }

    private void ViewModel_DiagnosticsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PreviewViewModel.SurfaceStatusText))
            UpdateSurfaceStatusVisibility();
    }

    private void UpdateSurfaceStatusVisibility()
    {
        if (_surfaceStatusTextBlock is null)
            return;

        _surfaceStatusTextBlock.Visibility = _viewModel.SurfaceStatusText.StartsWith(
            "Önizleme uyarısı:",
            StringComparison.Ordinal)
            ? Visibility.Collapsed
            : Visibility.Visible;
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
