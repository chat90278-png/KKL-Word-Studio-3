namespace KKL.WordStudio.UI;

using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using KKL.WordStudio.UI.ViewModels;
using KKL.WordStudio.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ExcelWorkspaceViewModel _excelWorkspaceViewModel;

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
        _excelWorkspaceViewModel = (ExcelWorkspaceViewModel)excelWorkspaceView.DataContext;
        DataContext = viewModel;

        LoadedSourcesHost.Content = loadedSourcesView;
        ExcelWorkspaceHost.Content = excelWorkspaceView;
        PreviewHost.Content = previewView;
        ContextDockHost.Content = contextDockView;

        _viewModel.DockViewModel.PropertyChanged += DockViewModel_PropertyChanged;
        _viewModel.ReportPane.PropertyChanged += ReportPane_PropertyChanged;
        _excelWorkspaceViewModel.PropertyChanged += ExcelWorkspaceViewModel_PropertyChanged;
        SizeChanged += MainWindow_SizeChanged;
        ContentRendered += MainWindow_ReportPaneContentRendered;

        ApplyDockColumnWidth();
        ApplyReportPaneState();
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

    private void DockViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockViewModel.State))
            ApplyDockColumnWidth();
    }

    private void ReportPane_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(ReportPaneViewModel.IsOpen) or nameof(ReportPaneViewModel.OpenWidth)))
            return;

        ApplyDockColumnWidth();
        ApplyReportPaneState();
    }

    private void ExcelWorkspaceViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ExcelWorkspaceViewModel.StatusText))
            return;

        var status = _excelWorkspaceViewModel.StatusText;
        if (!status.Contains('→'))
            return;

        if (status.Contains("önizlemeye eklendi", StringComparison.Ordinal)
            || status.EndsWith("güncellendi", StringComparison.Ordinal))
        {
            _viewModel.ReportPane.OpenForAction();
        }
    }

    private void ReportPaneToggleButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.ReportPane.Toggle();

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.ReportPane.ApplyViewportWidth(e.NewSize.Width);
        ApplyDockColumnWidth();
        ApplyReportPaneState();
    }

    private void MainWindow_ReportPaneContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= MainWindow_ReportPaneContentRendered;
        _viewModel.ReportPane.ApplyViewportWidth(ActualWidth);
        ApplyDockColumnWidth();
        ApplyReportPaneState();
    }

    private void ApplyDockColumnWidth()
    {
        // On narrow report panes the Context Dock remains part of the report
        // workspace but is physically compacted so Preview retains usable width.
        // The DockViewModel state itself is not overwritten; restoring room brings
        // the user's prior Normal/Expanded choice back automatically.
        if (!_viewModel.ReportPane.IsOpen || _viewModel.ReportPane.OpenWidth < 700)
        {
            DockColumn.Width = new GridLength(46);
            return;
        }

        DockColumn.Width = _viewModel.DockViewModel.State switch
        {
            DockState.Collapsed => new GridLength(46),
            DockState.Expanded => new GridLength(440),
            _ => new GridLength(350)
        };
    }

    private void ApplyReportPaneState()
    {
        if (_viewModel.ReportPane.IsOpen)
        {
            ReportPaneShell.Visibility = Visibility.Visible;
            ReportPaneShell.Width = _viewModel.ReportPane.OpenWidth;
            ReportPaneColumn.Width = GridLength.Auto;
            return;
        }

        // Collapse the actual grid column as well as the child. This avoids WPF's
        // Auto column measurement keeping Preview visible because its internal
        // columns have minimum widths. The existing view instances remain alive,
        // so selection, scroll position and rendered pages are preserved.
        ReportPaneShell.Width = 0;
        ReportPaneShell.Visibility = Visibility.Collapsed;
        ReportPaneColumn.Width = new GridLength(0);
    }
}
