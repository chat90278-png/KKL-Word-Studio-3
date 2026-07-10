namespace KKL.WordStudio.Application.Layout;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.ImportedDocuments;
using KKL.WordStudio.Application.Tables;

public sealed class DocumentLayoutRequest
{
    public required ReportContentDocument ReportContent { get; init; }
    public required ImportedDocumentPreviewDocument? FrontMatter { get; init; }
}

public sealed class DocumentLayoutResult
{
    public required IReadOnlyList<DocumentPageLayout> Pages { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class DocumentPageLayout
{
    public required int PageNumber { get; init; }
    public required DocumentPageOrigin Origin { get; init; }
    public required PageLayout PageLayout { get; init; }
    public required IReadOnlyList<PositionedPageBlock> Blocks { get; init; }
}

public enum DocumentPageOrigin
{
    FrontMatter,
    GeneratedReport
}

public sealed class PositionedPageBlock
{
    public required Guid? ElementId { get; init; }
    public required DocumentPageRegion Region { get; init; }
    public required PageBlockKind Kind { get; init; }
    public required double XMillimeters { get; init; }
    public required double YMillimeters { get; init; }
    public required double WidthMillimeters { get; init; }
    public required double HeightMillimeters { get; init; }
    public required int FragmentIndex { get; init; }
    public required bool IsContinuation { get; init; }
    public required bool IsEditableReportElement { get; init; }
    public required PageBlockPayload Payload { get; init; }
}

public enum DocumentPageRegion
{
    Header,
    Body,
    Footer
}

public enum PageBlockKind
{
    Text,
    Table,
    TableOfContents,
    Image,
    PageNumber,
    Unsupported
}

public abstract class PageBlockPayload
{
}

public sealed class TextPageBlockPayload : PageBlockPayload
{
    public required IReadOnlyList<TextRunLayout> Runs { get; init; }
    public required ReportContentKind? SemanticKind { get; init; }
    public required ParagraphAlignment Alignment { get; init; }
    public ResolvedTextFormat Format { get; init; } = DefaultFormatProfiles.BodyText;
}

public sealed class TextRunLayout
{
    public required string Text { get; init; }
    public required bool Bold { get; init; }
    public required bool Italic { get; init; }
    public required bool Underline { get; init; }
    public required double FontSizePoints { get; init; }
    public required string? FontFamilyName { get; init; }
}

public enum ParagraphAlignment
{
    Left,
    Center,
    Right,
    Justify
}

public sealed class TablePageBlockPayload : PageBlockPayload
{
    public required string Name { get; init; }
    public required string? Caption { get; init; }
    public required IReadOnlyList<string> ColumnHeaders { get; init; }
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    public IReadOnlyList<TableCellSpan> CellSpans { get; init; } = Array.Empty<TableCellSpan>();
    public ResolvedTableFormat Format { get; init; } = DefaultFormatProfiles.Table;
    public ResolvedTextFormat? CaptionFormat { get; init; }
    public required int StartRowIndex { get; init; }
    public required bool HasHeader { get; init; }
    public required bool IsHeaderRepeated { get; init; }
    public required string? SourceError { get; init; }
}

public sealed class TocPageBlockPayload : PageBlockPayload
{
    public required IReadOnlyList<LaidOutTocEntry> Entries { get; init; }
}

public sealed class LaidOutTocEntry
{
    public required Guid ElementId { get; init; }
    public required string Text { get; init; }
    public required int Level { get; init; }
    public required int PageNumber { get; init; }
}

public sealed class ImagePageBlockPayload : PageBlockPayload
{
    public required string Name { get; init; }
    public required byte[]? ImageBytes { get; init; }
    public required string? ContentType { get; init; }
    public required double? IntrinsicWidthMillimeters { get; init; }
    public required double? IntrinsicHeightMillimeters { get; init; }
}

public sealed class PageNumberPageBlockPayload : PageBlockPayload
{
    public required int PageNumber { get; init; }
}

public sealed class UnsupportedPageBlockPayload : PageBlockPayload
{
    public required string Description { get; init; }
}
