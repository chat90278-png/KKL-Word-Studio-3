namespace KKL.WordStudio.Application.Excel;

/// <summary>
/// UI-independent automatic range candidate produced from a bounded sheet
/// preview. The full-sheet reader resolves DataEndRow afterwards so preview
/// truncation can never silently become the product range end.
/// </summary>
public sealed class ExcelDataRangeCandidate
{
    public int? HeaderRowIndex { get; init; }
    public required int DataStartRow { get; init; }
    public required int StartColumn { get; init; }
    public required int EndColumn { get; init; }
    public required ExcelDataRangeConfidence Confidence { get; init; }

    public bool RequiresReview => Confidence == ExcelDataRangeConfidence.Low;
}

public enum ExcelDataRangeConfidence
{
    Low,
    High
}
