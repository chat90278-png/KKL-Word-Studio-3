namespace KKL.WordStudio.UI.Views;

using System.Windows.Controls;
using KKL.WordStudio.UI.ViewModels;

public partial class ContextDockView : UserControl
{
    public ContextDockView(
        DockViewModel dockViewModel,
        ContentsView contentsView,
        PropertiesView propertiesView,
        ChangeBindingView changeBindingView)
    {
        InitializeComponent();
        DataContext = dockViewModel;

        ContentsHost.Content = contentsView;
        PropertiesHost.Content = propertiesView;
        ChangeBindingHost.Content = changeBindingView;
    }
}
