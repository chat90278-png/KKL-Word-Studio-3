namespace KKL.WordStudio.Infrastructure.Word;

using DocumentFormat.OpenXml.Packaging;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Narrow DOCX front-matter importer: validates that the selected file is an
/// openable WordprocessingML package with a main document/body, then returns
/// project state. It never edits or rewrites the source file.
/// </summary>
public sealed class OpenXmlFrontMatterDocumentService : IFrontMatterDocumentService
{
    public bool IsAvailable(FrontMatterDocument frontMatter) =>
        FrontMatterSourcePathResolver.Resolve(frontMatter) is not null;

    public Result<FrontMatterDocument> Import(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".docx", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<FrontMatterDocument>("Ön belge olarak yalnızca .docx Word belgesi kullanılabilir.");

        if (!File.Exists(filePath))
            return Result.Failure<FrontMatterDocument>($"Ön belge bulunamadı: {filePath}");

        try
        {
            using var document = WordprocessingDocument.Open(filePath, false);
            if (document.MainDocumentPart?.Document?.Body is null)
                return Result.Failure<FrontMatterDocument>("Seçilen Word belgesinde okunabilir ana belge içeriği bulunamadı.");

            return Result.Success(new FrontMatterDocument
            {
                FileName = Path.GetFileName(filePath),
                OriginalSourcePath = filePath,
                ResolvedFilePath = filePath,
                EmbeddedAssetEntryName = FrontMatterDocument.DefaultEmbeddedAssetEntryName
            });
        }
        catch (Exception)
        {
            return Result.Failure<FrontMatterDocument>(
                $"'{Path.GetFileName(filePath)}' geçerli bir .docx ön belge olarak açılamadı.");
        }
    }
}
