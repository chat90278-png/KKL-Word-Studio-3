namespace KKL.WordStudio.UI.Services;

/// <summary>Abstraction over launching the OS shell (opening a file with its default app, or revealing a file in its containing folder) — kept separate from IFileDialogService since this is post-export "take me there" behavior, not a dialog.</summary>
public interface IShellLauncher
{
    void OpenFile(string filePath);
    void OpenContainingFolder(string filePath);
}
