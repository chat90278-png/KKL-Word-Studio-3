namespace KKL.WordStudio.UI.Services;

/// <summary>
/// Abstraction over the file dialogs that remain part of the Excel-first user
/// flow. Native project open/save dialogs were removed with the hidden project
/// lifecycle commands; Word and Excel dialogs stay unit-testable through this
/// UI-only contract.
/// </summary>
public interface IFileDialogService
{
    string? OpenExcelFile();
    string? OpenWordDocument();
    string? SaveWordFile(string suggestedFileName);
}
