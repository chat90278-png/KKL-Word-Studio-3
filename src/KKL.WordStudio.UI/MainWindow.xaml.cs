namespace KKL.WordStudio.UI;

using System.Windows;
using System.Windows.Media.Imaging;
using KKL.WordStudio.UI.ViewModels;
using KKL.WordStudio.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(
        MainViewModel viewModel,
        LoadedSourcesView loadedSourcesView,
        ExcelWorkspaceView excelWorkspaceView,
        PreviewView previewView,
        ContextDockView contextDockView)
    {
        InitializeComponent();
        ApplyRuntimeBrandIcon();

        _viewModel = viewModel;
        DataContext = viewModel;

        LoadedSourcesHost.Content = loadedSourcesView;
        ExcelWorkspaceHost.Content = excelWorkspaceView;
        PreviewHost.Content = previewView;
        ContextDockHost.Content = contextDockView;

        // ColumnDefinition is not a FrameworkElement and doesn't participate in
        // normal DataContext inheritance, so its Width can't be data-bound
        // directly from XAML — this is the standard WPF workaround: react to
        // the ViewModel's own PropertyChanged and set the width imperatively.
        // No product logic lives here, only a GridLength translation.
        _viewModel.DockViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DockViewModel.State))
                ApplyDockColumnWidth();
        };
        ApplyDockColumnWidth();
    }

    private void ApplyRuntimeBrandIcon()
    {
        try
        {
            Icon = BitmapFrame.Create(
                new Uri("pack://application:,,,/Assets/Brand/BrandMarkSmall.png", UriKind.Absolute));
        }
        catch
        {
            // Branding must never block application startup. The build-time
            // ApplicationIcon remains the safe fallback when a resource is missing.
        }
    }

    private void ApplyDockColumnWidth()
    {
        DockColumn.Width = _viewModel.DockViewModel.State switch
        {
            DockState.Collapsed => new GridLength(46),
            DockState.Expanded => new GridLength(440),
            _ => new GridLength(350)
        };
    }
}