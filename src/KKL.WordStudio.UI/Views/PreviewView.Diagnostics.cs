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
    private CancellationTokenSource? _navigationCancellation;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // PreviewView already has another partial lifecycle implementation.
        // Use template application only to attach the Loaded/Unloaded pair and
        // remove first so repeated template application cannot duplicate hooks.
        Loaded -= PreviewView_DiagnosticsLoaded;
        Unloaded -= PreviewView_DiagnosticsUnloaded;
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
        _navigationCancellation?.Cancel();
        _navigationCancellation?.Dispose();
        _navigationCancellation = null;
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

    private async void ViewModel_NavigateToElementRequested(Guid elementId)
    {
        _navigationCancellation?.Cancel();
        _navigationCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _navigationCancellation = cancellation;

        try
        {
            // A structure/edit command can request navigation while a newer Preview
            // generation is still replacing Pages. Keep the stable element Id and
            // resolve it only after the current projection publishes its target.
            for (var attempt = 0; attempt < 300; attempt++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var navigated = await Dispatcher.InvokeAsync(
                    () => BringElementIntoView(elementId),
                    DispatcherPriority.Loaded,
                    cancellation.Token);
                if (navigated)
                    return;

                await Task.Delay(100, cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer navigation request or view unload superseded this target.
        }
        finally
        {
            if (ReferenceEquals(_navigationCancellation, cancellation))
            {
                _navigationCancellation.Dispose();
                _navigationCancellation = null;
            }
        }
    }

    private bool BringElementIntoView(Guid elementId)
    {
        var page = _viewModel.Pages.FirstOrDefault(candidate =>
            candidate.Blocks.Any(block => block.ElementId == elementId));
        if (page is null)
            return false;

        var pagesControl = FindVisualDescendants<ItemsControl>(this)
            .FirstOrDefault(control => ReferenceEquals(control.ItemsSource, _viewModel.Pages));
        if (pagesControl is null)
            return false;

        pagesControl.UpdateLayout();
        if (pagesControl.ItemContainerGenerator.ContainerFromItem(page) is not FrameworkElement pageContainer)
            return false;

        pageContainer.BringIntoView();
        pageContainer.UpdateLayout();

        var blockHost = FindVisualDescendants<FrameworkElement>(pageContainer)
            .FirstOrDefault(element => element.DataContext is PreviewPageBlockViewModel block
                && block.ElementId == elementId);
        (blockHost ?? pageContainer).BringIntoView();
        Scroller.Focus();
        return true;
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
