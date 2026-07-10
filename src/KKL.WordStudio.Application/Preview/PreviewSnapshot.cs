namespace KKL.WordStudio.Application.Preview;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;

/// <summary>
/// A rendering-agnostic snapshot of a Report, built directly from
/// IReportContentBuilder's shared ReportContentDocument — the same
/// interpretation WordExporter consumes, mapped into UI-friendly block
/// lists per region instead of OpenXML.
/// </summary>
public sealed class PreviewSnapshot
{
    public required IReadOnlyList<PreviewBlock> HeaderBlocks { get; init; }
    public required IReadOnlyList<PreviewBlock> BodyBlocks { get; init; }
    public required IReadOnlyList<PreviewBlock> FooterBlocks { get; init; }
    public required IReadOnlyList<TocEntry> TableOfContents { get; init; }
    public required PageLayout PageLayout { get; init; }
    public required DocumentLayoutResult Layout { get; init; }
}

/// <summary>
/// Base preview block. Split into Text/Table variants (Sprint 6) mirroring
/// TextContentNode/TableContentNode exactly — a flat "join cells with |"
/// text summary was the reason tables didn't look like real tables in
/// Preview. Keeping structured data here lets the UI render an actual grid
/// instead of reformatting text back into columns.
/// </summary>
public abstract class PreviewBlock
{
    public required Guid ElementId { get; init; }
}

public sealed class TextPreviewBlock : PreviewBlock
{
    public required ReportContentKind Kind { get; init; }
    public required string Text { get; init; }
}

public sealed class TablePreviewBlock : PreviewBlock
{
    public required string Name { get; init; }
    public string? Caption { get; init; }
    public required IReadOnlyList<string> ColumnHeaders { get; init; }
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    public string? DataSourceName { get; init; }
    public int SourceCount { get; init; }
    public string? SourceError { get; init; }
    public bool FilterWasIgnored { get; init; }
}
