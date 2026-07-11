namespace KKL.WordStudio.UI.Views;

using System.Windows.Controls;
using KKL.WordStudio.UI.ViewModels;

public partial class WarningCenterView : UserControl
{
    public WarningCenterView(WarningCenterViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
