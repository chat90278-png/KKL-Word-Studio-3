namespace KKL.WordStudio.Infrastructure.Export.Composition;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;

/// <summary>
/// Composes a whole imported DOCX before generated KKL content using the
/// OpenXML alternative-format import mechanism (w:altChunk). The source DOCX
/// remains a separate package part; its styles, numbering, images and internal
/// relationships are not cloned into the host package with raw Body children,
/// avoiding relationship/style-id collision work in this Sprint 8 foundation.
/// </summary>
internal static class WordFrontMatterComposer
{
    public static void AppendFrontMatter(MainDocumentPart mainPart, Body body, string sourceDocxPath)
    {
        var relationshipId = $"FrontMatter_{Guid.NewGuid():N}";
        var importPart = mainPart.AddAlternativeFormatImportPart(
            AlternativeFormatImportPartType.WordprocessingML,
            relationshipId);

        using (var sourceStream = File.Open(sourceDocxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            importPart.FeedData(sourceStream);

        body.AppendChild(new AltChunk { Id = relationshipId });
        body.AppendChild(WordParagraphWriter.BuildPageBreakParagraph());
    }
}
