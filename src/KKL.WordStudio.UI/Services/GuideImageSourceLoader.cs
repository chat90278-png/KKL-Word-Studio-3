namespace KKL.WordStudio.UI.Services;

using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Loads the approved guide screens from embedded Base64 resources and
/// user-selected replacements from disk. Bitmap data is fully cached in memory,
/// so neither the executable resource stream nor a custom image file stays locked.
/// </summary>
public sealed class GuideImageSourceLoader
{
    private readonly Assembly _assembly = typeof(GuideImageSourceLoader).Assembly;
    private readonly Dictionary<string, ImageSource?> _embeddedCache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? Load(string assetName) => LoadEmbedded(assetName);

    public ImageSource? LoadEmbedded(string assetName)
    {
        if (_embeddedCache.TryGetValue(assetName, out var cached))
            return cached;

        var resourceName = _assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($".{assetName}.base64", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return _embeddedCache[assetName] = null;

        try
        {
            using var resource = _assembly.GetManifestResourceStream(resourceName);
            if (resource is null)
                return _embeddedCache[assetName] = null;

            using var reader = new StreamReader(resource);
            var bytes = DecodeBase64Resource(reader.ReadToEnd());
            return _embeddedCache[assetName] = CreateBitmap(bytes);
        }
        catch (FormatException)
        {
            return _embeddedCache[assetName] = null;
        }
        catch (NotSupportedException)
        {
            return _embeddedCache[assetName] = null;
        }
        catch (IOException)
        {
            return _embeddedCache[assetName] = null;
        }
    }

    public ImageSource? LoadFile(string path)
    {
        try
        {
            return File.Exists(path) ? CreateBitmap(File.ReadAllBytes(path)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    internal static byte[] DecodeBase64Resource(string text)
    {
        var encoded = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
                continue;

            if (char.IsAsciiLetterOrDigit(character) || character is '+' or '/' or '=')
                encoded.Append(character);
        }

        var normalized = encoded.ToString();
        var paddingIndex = normalized.IndexOf('=');
        if (paddingIndex >= 0)
        {
            var end = paddingIndex + 1;
            if (end < normalized.Length && normalized[end] == '=') end++;
            normalized = normalized[..end];
        }

        return Convert.FromBase64String(normalized);
    }

    private static ImageSource CreateBitmap(byte[] bytes)
    {
        using var imageStream = new MemoryStream(bytes, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.StreamSource = imageStream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
