namespace KKL.WordStudio.UI.Services;

using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Loads the approved guide screenshots from embedded JPEG resources and
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
            .FirstOrDefault(name => name.EndsWith($".{assetName}", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return _embeddedCache[assetName] = null;

        try
        {
            using var resource = _assembly.GetManifestResourceStream(resourceName);
            if (resource is null)
                return _embeddedCache[assetName] = null;

            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            return _embeddedCache[assetName] = CreateBitmap(buffer.ToArray());
        }
        catch (IOException)
        {
            return _embeddedCache[assetName] = null;
        }
        catch (NotSupportedException)
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
