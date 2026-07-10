namespace KKL.WordStudio.Application.Excel;

using KKL.WordStudio.Shared.Results;

/// <summary>Deterministic, UI-independent heuristic for locating a tabular block in a raw sheet preview.</summary>
public interface IExcelDataRangeDetector
{
    Result<ExcelDataRangeCandidate> Detect(SheetPreview preview);
}
