namespace KKL.WordStudio.UI.Views;

using System.Windows.Controls;
using KKL.WordStudio.UI.ViewModels;

public partial class UsageGuideView : UserControl
{
    public UsageGuideView(UsageGuideViewModel viewModel, MainViewModel mainViewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        CloseGuideButton.Command = mainViewModel.CloseUsageGuideCommand;
    }
}
