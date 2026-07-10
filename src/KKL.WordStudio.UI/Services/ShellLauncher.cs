namespace KKL.WordStudio.UI.Services;

using System.Diagnostics;
using System.IO;

public sealed class ShellLauncher : IShellLauncher
{
    public void OpenFile(string filePath) =>
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

    public void OpenContainingFolder(string filePath)
    {
        var folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(folder)) return;

        // /select, highlights the exported file itself rather than just opening the folder.
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
    }
}
