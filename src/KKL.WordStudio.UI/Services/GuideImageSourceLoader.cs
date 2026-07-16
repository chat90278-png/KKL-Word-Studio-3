namespace KKL.WordStudio.UI.Services;

using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Loads guide screenshots from embedded Base64 resources. Screens are stored
/// in ordered text chunks so repository writes and single-file publishing stay
/// reliable without creating loose runtime files.
/// </summary>
public sealed class GuideImageSourceLoader
{
    private readonly Assembly _assembly = typeof(GuideImageSourceLoader).Assembly;
    private readonly Dictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? Load(string assetName)
    {
        if (_cache.TryGetValue(assetName, out var cached))
            return cached;

        var resourceNames = _assembly.GetManifestResourceNames();
        var chunkMarker = $".{assetName}.part";
        var chunks = resourceNames
            .Where(name => name.Contains(chunkMarker, StringComparison.OrdinalIgnoreCase)
                           && name.EndsWith(".base64", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (chunks.Length == 0)
            return _cache[assetName] = null;

        try
        {
            var encoded = new StringBuilder();
            foreach (var chunk in chunks)
            {
                using var resource = _assembly.GetManifestResourceStream(chunk);
                if (resource is null)
                    return _cache[assetName] = null;

                using var reader = new StreamReader(resource);
                encoded.Append(reader.ReadToEnd().Trim());
            }

            var bytes = Convert.FromBase64String(encoded.ToString());
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
