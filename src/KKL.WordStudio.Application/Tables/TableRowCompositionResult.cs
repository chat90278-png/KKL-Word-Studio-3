namespace KKL.WordStudio.Application.Tables;

public sealed class TableRowCompositionResult
{
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    public required IReadOnlyList<TableCellSpan> CellSpans { get; init; }
    public required IReadOnlyList<TableRowGroup> RowGroups { get; init; }

    /// <summary>
    /// Legacy technical messages retained for frozen contracts and support logs.
    /// New consumers should use <see cref="Diagnostics"/>.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Structured composition findings. Classification occurs once at the
    /// composition boundary instead of being repeated by Preview and UI code.
    /// </summary>
    public IReadOnlyList<TableCompositionDiagnostic> Diagnostics =>
        TableCompositionDiagnosticClassifier.Classify(Warnings);
}
