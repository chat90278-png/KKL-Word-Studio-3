namespace KKL.WordStudio.UI.Services;

public enum ExportWarningDecision
{
    Continue,
    Review,
    Cancel
}

/// <summary>Abstraction over WPF dialogs so ViewModels never touch System.Windows types directly.</summary>
public interface IDialogService
{
    void ShowError(string message, string title = "Error");
    bool ShowConfirmation(string message, string title = "Confirm");
    ExportWarningDecision ShowExportWarningDecision(string message, string title = "Uyarılarla Devam Et");
}
