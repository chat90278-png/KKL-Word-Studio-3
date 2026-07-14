namespace KKL.WordStudio.UI.Services;

using Microsoft.Win32;

public sealed class FileDialogService : IFileDialogService
{
    public string? OpenExcelFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel Dosyaları (*.xlsx;*.xlsm)|*.xlsx;*.xlsm",
            Title = "Excel Dosyası Aç"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? OpenWordDocument()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Word Belgesi (*.docx)|*.docx",
            Title = "Ön Belge Ekle"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveWordFile(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Word Belgesi (*.docx)|*.docx",
            FileName = suggestedFileName,
            Title = "Word Dosyası Oluştur"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
