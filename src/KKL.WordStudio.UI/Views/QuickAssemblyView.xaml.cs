namespace KKL.WordStudio.UI.Views;

using System.Windows.Controls;
using KKL.WordStudio.UI.ViewModels;

public partial class QuickAssemblyView : UserControl
{
    public QuickAssemblyView(QuickAssemblyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
