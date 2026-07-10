namespace KKL.WordStudio.Application.Tables;

using KKL.WordStudio.Domain.Elements;

public interface ITableContentRowComposer
{
    TableRowCompositionResult Compose(
        TableElement table,
        IReadOnlyList<IReadOnlyList<string>> normalizedRows);
}
