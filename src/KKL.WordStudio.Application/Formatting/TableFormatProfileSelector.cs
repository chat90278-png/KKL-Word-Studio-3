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

        if (profile is null || profile.TableFormats.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(table.ReferenceTableFormatKey))
        {
            var explicitSelection = profile.TableFormats.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, table.ReferenceTableFormatKey, StringComparison.Ordinal));
            if (explicitSelection is not null)
                return explicitSelection;
        }

        return profile.TableFormats.FirstOrDefault(candidate =>
                   candidate.ReferenceHeaders.Count == table.Columns.Count)
               ?? profile.TableFormats.FirstOrDefault();
    }
}
