namespace KKL.WordStudio.UI.Services;

using System.IO;
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
    private static readonly byte[] PngEndMarker = [0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];

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
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("Guide image resource is empty.");

        var encoded = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsAsciiLetterOrDigit(character) || character is '+' or '/' or '=')
                encoded.Append(character);
        }

        var normalized = encoded.ToString();
        var jpegStart = normalized.IndexOf("/9j/", StringComparison.Ordinal);
        var pngStart = normalized.IndexOf("iVBORw0KGgo", StringComparison.Ordinal);
        var start = jpegStart < 0
            ? pngStart
            : pngStart < 0
                ? jpegStart
                : Math.Min(jpegStart, pngStart);

        if (start < 0)
            throw new FormatException("Guide image resource does not contain a JPEG or PNG payload.");

        normalized = normalized[start..];

        // Repository text transports may append metadata after the payload. Remove
        // existing padding and rebuild it after isolating the known image prefix.
        var paddingIndex = normalized.IndexOf('=');
        if (paddingIndex >= 0)
            normalized = normalized[..paddingIndex];

        while (normalized.Length > 0 && normalized.Length % 4 == 1)
            normalized = normalized[..^1];

        if (normalized.Length == 0)
            throw new FormatException("Guide image resource has no decodable payload.");

        var requiredPadding = (4 - normalized.Length % 4) % 4;
        if (requiredPadding > 0)
            normalized = normalized.PadRight(normalized.Length + requiredPadding, '=');

        return TrimToImagePayload(Convert.FromBase64String(normalized));
    }

    private static byte[] TrimToImagePayload(byte[] bytes)
    {
        if (IsJpeg(bytes))
        {
            for (var index = 2; index < bytes.Length - 1; index++)
            {
                if (bytes[index] == 0xFF && bytes[index + 1] == 0xD9)
                    return bytes[..(index + 2)];
            }
        }
        else if (IsPng(bytes))
        {
            var markerIndex = bytes.AsSpan().IndexOf(PngEndMarker);
            if (markerIndex >= 0)
                return bytes[..(markerIndex + PngEndMarker.Length)];
        }

        throw new FormatException("Guide image resource is truncated or unsupported.");
    }

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsPng(byte[] bytes) =>
        bytes.Length >= 8
        && bytes[0] == 0x89
        && bytes[1] == 0x50
        && bytes[2] == 0x4E
        && bytes[3] == 0x47;

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
