namespace KKL.WordStudio.Application.TableComposition;

using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Shared.Results;

public interface ISerialQuantityGroupingConfigurationService
{
    SerialQuantityGroupingDiagnosis Diagnose(TableElement table);
    Result ApplyManual(TableElement table, Guid matchKeyColumnId, Guid serialColumnId, Guid quantityColumnId);
    Result AutoDetect(TableElement table);
    Result Remove(TableElement table);
}
