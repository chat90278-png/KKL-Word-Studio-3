namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Domain.Elements;

/// <summary>
/// Resolves the effective table-format profile without mutating the authored
/// table selection. A null table key remains the persisted "automatic" state;
/// callers may use the returned profile for rendering or for explaining the
/// effective automatic choice in authoring UI.
/// </summary>
public static class TableFormatProfileSelector
{
    public static ReferenceTableFormatProfile? Select(
        DocumentFormatProfile? profile,
        TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return Select(profile, table.Columns.Count, table.ReferenceTableFormatKey);
    }

    public static ReferenceTableFormatProfile? SelectAutomatic(
        DocumentFormatProfile? profile,
        TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return Select(profile, table.Columns.Count, referenceTableFormatKey: null);
    }

    private static ReferenceTableFormatProfile? Select(
        DocumentFormatProfile? profile,
        int columnCount,
        string? referenceTableFormatKey)
    {
        if (profile is null || profile.TableFormats.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(referenceTableFormatKey))
        {
            var explicitSelection = profile.TableFormats.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, referenceTableFormatKey, StringComparison.Ordinal));
            if (explicitSelection is not null)
                return explicitSelection;
        }

        return profile.TableFormats.FirstOrDefault(candidate =>
                   candidate.ReferenceHeaders.Count == columnCount)
               ?? profile.TableFormats.FirstOrDefault();
    }
}
