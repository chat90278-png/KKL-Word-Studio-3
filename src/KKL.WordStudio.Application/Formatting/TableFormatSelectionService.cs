namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Shared.Results;

public sealed class TableFormatSelectionService : ITableFormatSelectionService
{
    public Result Apply(TableElement table, DocumentFormatProfile? profile, string? referenceTableFormatKey)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (string.IsNullOrWhiteSpace(referenceTableFormatKey))
        {
            table.ReferenceTableFormatKey = null;
            return Result.Success();
        }

        if (profile is null || !profile.TableFormats.Any(candidate =>
                string.Equals(candidate.Key, referenceTableFormatKey, StringComparison.Ordinal)))
        {
            return Result.Failure("Seçilen referans tablo biçimi kullanılamıyor.");
        }

        table.ReferenceTableFormatKey = referenceTableFormatKey;
        return Result.Success();
    }
}
