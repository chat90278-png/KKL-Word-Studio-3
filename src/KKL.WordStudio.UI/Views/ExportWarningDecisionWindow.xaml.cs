namespace KKL.WordStudio.UI.Views;

using System.Windows;
using KKL.WordStudio.UI.Services;

public partial class ExportWarningDecisionWindow : Window
{
    public ExportWarningDecisionWindow(string message, string title)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
    }

    public ExportWarningDecision Decision { get; private set; } = ExportWarningDecision.Cancel;

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        Decision = ExportWarningDecision.Continue;
        DialogResult = true;
    }

    private void Review_Click(object sender, RoutedEventArgs e)
    {
        Decision = ExportWarningDecision.Review;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Decision = ExportWarningDecision.Cancel;
        DialogResult = false;
    }
}
