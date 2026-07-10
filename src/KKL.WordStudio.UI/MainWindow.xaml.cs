namespace KKL.WordStudio.UI;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KKL.WordStudio.UI.ViewModels;
using KKL.WordStudio.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(
        MainViewModel viewModel,
        ProjectExplorerView projectExplorerView,
        ExcelWorkspaceView excelWorkspaceView,
        PreviewView previewView,
        ContextDockView contextDockView)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        ProjectExplorerHost.Content = projectExplorerView;
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

    private void ApplyDockColumnWidth()
    {
        DockColumn.Width = _viewModel.DockViewModel.State switch
        {
            DockState.Collapsed => new GridLength(46),
            DockState.Expanded => new GridLength(440),
            _ => new GridLength(350)
        };
    }

    // Purely visual: clicking the dimmed scrim behind the Project Explorer
    // overlay closes it. No product logic — just toggles the existing
    // IsProjectExplorerOpen flag the rail button also controls.
    private void ProjectExplorerScrim_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _viewModel.ToggleProjectExplorerCommand.Execute(null);
}
