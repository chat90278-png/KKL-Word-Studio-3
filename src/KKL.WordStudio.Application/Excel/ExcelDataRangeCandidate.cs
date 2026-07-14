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

    /// <summary>
    /// Canonical fields recognised on the selected header row. Empty when the
    /// candidate has no header or no supported semantic aliases were found.
    /// </summary>
    public IReadOnlyList<ExcelSemanticFieldMatch> SemanticFields { get; init; } =
        Array.Empty<ExcelSemanticFieldMatch>();

    public int SemanticMatchCount => SemanticFields.Count;
    public bool RequiresReview => Confidence == ExcelDataRangeConfidence.Low;
}

public enum ExcelDataRangeConfidence
{
    Low,
    High
}
