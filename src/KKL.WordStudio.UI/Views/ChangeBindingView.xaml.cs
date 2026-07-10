namespace KKL.WordStudio.UI.Views;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KKL.WordStudio.UI.ViewModels;

public partial class ChangeBindingView : UserControl
{
    public ChangeBindingView(PropertiesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    // Purely visual: FrameworkElement.DataContext resolution for a click on a
    // templated row is simpler here than plumbing a Command+CommandParameter
    // through a Border (Border has no ICommandSource). No product logic lives
    // in this handler — it only forwards the clicked item to the existing
    // SelectBindingCandidateCommand.
    private void CandidateBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BindingCandidateViewModel candidate } &&
            DataContext is PropertiesViewModel viewModel)
        {
            viewModel.SelectBindingCandidateCommand.Execute(candidate);
        }
    }
}
