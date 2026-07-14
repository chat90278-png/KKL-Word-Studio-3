namespace KKL.WordStudio.UI.Services;

using System.Windows;

public sealed class DialogService : IDialogService
{
    public void ShowError(string message, string title = "Error")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool ShowConfirmation(string message, string title = "Confirm")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public ExportWarningDecision ShowExportWarningDecision(string message, string title = "Uyarılarla Devam Et")
    {
        var result = MessageBox.Show(
            $"{message}\n\nEvet: Word dosyasını oluştur\nHayır: Kontrol merkezine git\nİptal: İşlemi durdur",
            title,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => ExportWarningDecision.Continue,
            MessageBoxResult.No => ExportWarningDecision.Review,
            _ => ExportWarningDecision.Cancel
        };
    }
}
