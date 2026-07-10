namespace KKL.WordStudio.Application.TableComposition;

using KKL.WordStudio.Domain.Elements;

public interface ISerialQuantityGroupingDetector
{
    SerialQuantityGrouping? Detect(IReadOnlyList<TableColumn> columns);
}
