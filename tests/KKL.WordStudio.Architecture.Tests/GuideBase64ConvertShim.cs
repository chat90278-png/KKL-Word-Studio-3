namespace KKL.WordStudio.Architecture.Tests;

using System.Text;

/// <summary>
/// Keeps architecture checks strict about the decoded image while tolerating
/// harmless text-transport artifacts around Base64 resources.
/// </summary>
internal static class Convert
{
    public static byte[] FromBase64String(string value)
    {
        try
        {
            return System.Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            var encoded = new StringBuilder(value.Length);
            foreach (var character in value)
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
                throw;

            normalized = normalized[start..];
            var paddingIndex = normalized.IndexOf('=');
            if (paddingIndex >= 0)
                normalized = normalized[..paddingIndex];

            while (normalized.Length > 0 && normalized.Length % 4 == 1)
                normalized = normalized[..^1];

            var requiredPadding = (4 - normalized.Length % 4) % 4;
            if (requiredPadding > 0)
                normalized = normalized.PadRight(normalized.Length + requiredPadding, '=');

            return System.Convert.FromBase64String(normalized);
        }
    }
}
