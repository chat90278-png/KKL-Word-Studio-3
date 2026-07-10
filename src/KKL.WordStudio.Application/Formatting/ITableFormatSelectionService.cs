namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Shared.Results;

public interface ITableFormatSelectionService
{
    Result Apply(TableElement table, DocumentFormatProfile? profile, string? referenceTableFormatKey);
}
