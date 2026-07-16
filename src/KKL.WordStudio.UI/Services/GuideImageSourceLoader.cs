namespace KKL.WordStudio.UI.Services;

using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Loads guide screenshots from embedded Base64 resources. Keeping the source
/// images as text allows the repository and single-file publish pipeline to
/// carry the real screenshots without creating loose runtime files.
/// </summary>
public sealed class GuideImageSourceLoader
{
    private readonly Assembly _assembly = typeof(GuideImageSourceLoader).Assembly;
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? Load(string assetName)
    {
        if (_cache.TryGetValue(assetName, out var cached))
            return cached;

        var suffix = $".{assetName}.base64";
        var resourceName = _assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return _cache[assetName] = null;

        try
        {
            using var resource = _assembly.GetManifestResourceStream(resourceName);
            if (resource is null)
                return _cache[assetName] = null;

            using var reader = new StreamReader(resource);
            var bytes = Convert.FromBase64String(reader.ReadToEnd().Trim());
            using var imageStream = new MemoryStream(bytes, writable: false);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.StreamSource = imageStream;
            bitmap.EndInit();
            bitmap.Freeze();

            return _cache[assetName] = bitmap;
        }
        catch (FormatException)
        {
            return _cache[assetName] = null;
        }
        catch (NotSupportedException)
        {
            return _cache[assetName] = null;
        }
    }
}
