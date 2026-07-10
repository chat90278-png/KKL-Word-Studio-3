namespace KKL.WordStudio.UI.ViewModels;

using Microsoft.Win32;

/// <summary>UI-only picker kept separate from the existing front-matter command path.</summary>
internal static class ReferenceFormatFilePicker
{
    public static string? PickDocx()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Word Belgesi (*.docx)|*.docx",
            Title = "Biçim Şablonu Seç"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
