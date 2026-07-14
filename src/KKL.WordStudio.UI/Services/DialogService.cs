namespace KKL.WordStudio.UI.Services;

using System.Windows;
using KKL.WordStudio.UI.Views;

public sealed class DialogService : IDialogService
{
    public void ShowError(string message, string title = "Error")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool ShowConfirmation(string message, string title = "Confirm")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public ExportWarningDecision ShowExportWarningDecision(string message, string title = "Uyarılarla Devam Et")
    {
        var dialog = new ExportWarningDecisionWindow(message, title);
        if (Application.Current?.MainWindow is { } owner && !ReferenceEquals(owner, dialog))
            dialog.Owner = owner;

        dialog.ShowDialog();
        return dialog.Decision;
    }
}
