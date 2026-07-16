namespace KKL.WordStudio.UI.Services;

using System.IO;
using System.Text.Json;
using KKL.WordStudio.UI.Models;
using Microsoft.Win32;

public sealed class UsageGuideContentStore
{
    private const int DocumentVersion = 1;
    private const string ProductFolderName = "KKL Word Studio";
    private readonly string _rootDirectory;
    private readonly string _imageDirectory;
    private readonly string _documentPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public UsageGuideContentStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductFolderName,
            "UsageGuide"))
    {
    }

    internal UsageGuideContentStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        _imageDirectory = Path.Combine(rootDirectory, "Images");
        _documentPath = Path.Combine(rootDirectory, "usage-guide.json");
    }

    public IReadOnlyDictionary<string, UsageGuideSectionOverride> LoadOverrides() =>
        new Dictionary<string, UsageGuideSectionOverride>(
            ReadDocument().Sections,
            StringComparer.OrdinalIgnoreCase);

    public string? PickImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Kılavuz ekran görselini seç",
            Filter = "Görsel dosyaları|*.png;*.jpg;*.jpeg;*.bmp|PNG|*.png|JPEG|*.jpg;*.jpeg|Bitmap|*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public UsageGuideSectionOverride Save(
        string sectionId,
        string title,
        string purpose,
        IReadOnlyList<string> actions,
        string tip,
        string? selectedImagePath,
        bool useDefaultImage)
    {
        var document = ReadDocument();
        document.Sections.TryGetValue(sectionId, out var existing);

        var customImageFileName = existing?.CustomImageFileName;
        if (useDefaultImage)
        {
            DeleteCustomImage(customImageFileName);
            customImageFileName = null;
        }
        else if (!string.IsNullOrWhiteSpace(selectedImagePath))
        {
            customImageFileName = CopyCustomImage(sectionId, selectedImagePath, customImageFileName);
        }

        var content = new UsageGuideSectionOverride(
            title.Trim(),
            purpose.Trim(),
            actions.Select(action => action.Trim()).Where(action => action.Length > 0).ToArray(),
            tip.Trim(),
            customImageFileName);

        document.Sections[sectionId] = content;
        WriteDocument(document);
        return content;
    }

    public void Reset(string sectionId)
    {
        var document = ReadDocument();
        if (!document.Sections.Remove(sectionId, out var existing))
            return;

        DeleteCustomImage(existing.CustomImageFileName);
        WriteDocument(document);
    }

    public string? ResolveCustomImagePath(UsageGuideSectionOverride? content)
    {
        if (string.IsNullOrWhiteSpace(content?.CustomImageFileName))
            return null;

        var fileName = Path.GetFileName(content.CustomImageFileName);
        var path = Path.Combine(_imageDirectory, fileName);
        return File.Exists(path) ? path : null;
    }

    private UsageGuideOverrideDocument ReadDocument()
    {
        if (!File.Exists(_documentPath))
            return EmptyDocument();

        try
        {
            var json = File.ReadAllText(_documentPath);
            var document = JsonSerializer.Deserialize<UsageGuideOverrideDocument>(json, _jsonOptions);
            return document is null
                ? EmptyDocument()
                : document with
                {
                    Sections = new Dictionary<string, UsageGuideSectionOverride>(
                        document.Sections,
                        StringComparer.OrdinalIgnoreCase)
                };
        }
        catch (IOException)
        {
            return EmptyDocument();
        }
        catch (JsonException)
        {
            return EmptyDocument();
        }
        catch (UnauthorizedAccessException)
        {
            return EmptyDocument();
        }
    }

    private void WriteDocument(UsageGuideOverrideDocument document)
    {
        Directory.CreateDirectory(_rootDirectory);
        var temporaryPath = _documentPath + ".tmp";
        var json = JsonSerializer.Serialize(document with { Version = DocumentVersion }, _jsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, _documentPath, overwrite: true);
    }

    private string CopyCustomImage(string sectionId, string sourcePath, string? previousFileName)
    {
        if (!File.Exists(sourcePath))
            throw new IOException("Seçilen görsel dosyası bulunamadı.");

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension == ".jpeg") extension = ".jpg";
        if (extension is not (".png" or ".jpg" or ".bmp"))
            throw new InvalidOperationException("Yalnız PNG, JPG veya BMP görselleri kullanılabilir.");

        Directory.CreateDirectory(_imageDirectory);
        var targetFileName = $"{SanitizeSectionId(sectionId)}{extension}";
        var targetPath = Path.Combine(_imageDirectory, targetFileName);
        var sourceFullPath = Path.GetFullPath(sourcePath);
        var targetFullPath = Path.GetFullPath(targetPath);

        if (!sourceFullPath.Equals(targetFullPath, StringComparison.OrdinalIgnoreCase))
            File.Copy(sourceFullPath, targetFullPath, overwrite: true);

        if (!string.IsNullOrWhiteSpace(previousFileName)
            && !previousFileName.Equals(targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            DeleteCustomImage(previousFileName);
        }

        return targetFileName;
    }

    private void DeleteCustomImage(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var path = Path.Combine(_imageDirectory, Path.GetFileName(fileName));
        if (File.Exists(path))
            File.Delete(path);
    }

    private static string SanitizeSectionId(string sectionId) =>
        new(sectionId
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-')
            .ToArray());

    private static UsageGuideOverrideDocument EmptyDocument() =>
        new(DocumentVersion, new Dictionary<string, UsageGuideSectionOverride>(StringComparer.OrdinalIgnoreCase));
}
