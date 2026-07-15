namespace KKL.WordStudio.Application.Content;

using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Tables;

/// <summary>
/// Format-agnostic interpretation of one piece of report content, produced
/// once by ReportContentBuilder and consumed by both the Preview renderer
/// and WordExporter. Neither consumer re-derives "is this a heading" or
/// "what rows does this bound table actually have" — that work happens
/// exactly once, here.
/// </summary>
public abstract class ReportContentNode
{
    public required Guid ElementId { get; init; }
    public required ReportContentKind Kind { get; init; }
}

public sealed class TextContentNode : ReportContentNode
{
    public required string Text { get; init; }
    public bool Bold { get; init; }
    public double FontSize { get; init; }
    public ResolvedTextFormat Format { get; init; } = DefaultFormatProfiles.BodyText;
}

public sealed class TableContentNode : ReportContentNode
{
    public required string Name { get; init; }
    public string? Caption { get; init; }
    public required IReadOnlyList<string> ColumnHeaders { get; init; }
    public ResolvedTableFormat Format { get; init; } = DefaultFormatProfiles.Table;
    public ResolvedTextFormat? CaptionFormat { get; init; }
    public TableCaptionSequenceProfile? CaptionSequence { get; init; }

    /// <summary>Resolved cell text, outer = rows, inner = columns (same order as ColumnHeaders). For a bound table, these came from IDataProvider + Binding.SortFields, applied once here.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Complete-table semantic vertical spans. RowIndex is relative to <see cref="Rows"/>.</summary>
    public IReadOnlyList<TableCellSpan> CellSpans { get; init; } = Array.Empty<TableCellSpan>();

    /// <summary>Complete-table semantic row groups consumed by the layout engine.</summary>
    public IReadOnlyList<TableRowGroup> RowGroups { get; init; } = Array.Empty<TableRowGroup>();

    /// <summary>
    /// Legacy technical warning messages retained for renderer/export compatibility.
    /// New diagnostic consumers should use <see cref="CompositionDiagnostics"/>.
    /// </summary>
    public IReadOnlyList<string> CompositionWarnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Stable structured projection of composition findings. The conversion is
    /// owned by Application and is shared by Preview, navigation and export guards.
    /// </summary>
    public IReadOnlyList<TableCompositionDiagnostic> CompositionDiagnostics =>
        TableCompositionDiagnosticClassifier.Classify(CompositionWarnings);

    public string? DataSourceName { get; init; }
    public int SourceCount { get; init; }

    /// <summary>Friendly composition error for a persisted but unusable multi-source input. Preview surfaces it; Word export refuses incomplete output.</summary>
    public string? SourceError { get; init; }

    /// <summary>True if this table is bound but its Filter could not be applied (no expression evaluator exists yet — see ADR 0006). Surfaced so consumers can show/log that rows are unfiltered rather than silently dropping the filter.</summary>
    public bool FilterWasIgnored { get; init; }
}

public sealed class ImageContentNode : ReportContentNode
{
    public required string Name { get; init; }
}
