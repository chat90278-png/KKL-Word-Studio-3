namespace KKL.WordStudio.Application.ImportedDocuments;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;

public sealed class ImportedDocumentPreviewResult
{
    public required ImportedDocumentPreviewDocument? Document { get; init; }
    public required bool IsMissing { get; init; }
    public required string? StatusMessage { get; init; }
}

public sealed class ImportedDocumentPreviewDocument
{
    public required IReadOnlyList<ImportedDocumentSection> Sections { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class ImportedDocumentSection
{
    public required PageLayout PageLayout { get; init; }
    public required IReadOnlyList<ImportedDocumentBlock> Blocks { get; init; }
}

public abstract class ImportedDocumentBlock
{
}

public sealed class ImportedParagraphBlock : ImportedDocumentBlock
{
    public required IReadOnlyList<ImportedTextRun> Runs { get; init; }
    public required ParagraphAlignment Alignment { get; init; }
    public required bool KeepWithNext { get; init; }
}

public sealed class ImportedTextRun
{
    public required string Text { get; init; }
    public required bool Bold { get; init; }
    public required bool Italic { get; init; }
    public required bool Underline { get; init; }
    public required double FontSizePoints { get; init; }
    public required string? FontFamilyName { get; init; }
}

public sealed class ImportedTableBlock : ImportedDocumentBlock
{
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    public required bool RepeatFirstRow { get; init; }
}

public sealed class ImportedImageBlock : ImportedDocumentBlock
{
    public required string Name { get; init; }
    public required byte[] ImageBytes { get; init; }
    public required string ContentType { get; init; }
    public required double? WidthMillimeters { get; init; }
    public required double? HeightMillimeters { get; init; }
}

public sealed class ImportedExplicitPageBreakBlock : ImportedDocumentBlock
{
}

public sealed class ImportedUnsupportedBlock : ImportedDocumentBlock
{
    public required string Description { get; init; }
}
