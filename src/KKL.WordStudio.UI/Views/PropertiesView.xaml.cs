namespace KKL.WordStudio.UI.Views;

using System.Windows.Controls;
using KKL.WordStudio.UI.ViewModels;

public partial class PropertiesView : UserControl
{
    public PropertiesView(PropertiesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
