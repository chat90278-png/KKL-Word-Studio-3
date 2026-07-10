namespace KKL.WordStudio.Infrastructure.ReferenceFormatting;

using DocumentFormat.OpenXml.Packaging;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Validates and registers a DOCX as a project reference-format asset. The
/// package is opened read-only and is never rewritten during import.
/// </summary>
public sealed class OpenXmlReferenceFormatDocumentService : IReferenceFormatDocumentService
{
    public bool IsAvailable(ReferenceFormatDocument referenceFormat) =>
        ReferenceFormatSourcePathResolver.Resolve(referenceFormat) is not null;

    public Result<ReferenceFormatDocument> Import(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ReferenceFormatDocument>(
                "Biçim şablonu olarak yalnızca .docx Word belgesi kullanılabilir.");
        }

        if (!File.Exists(filePath))
            return Result.Failure<ReferenceFormatDocument>($"Biçim şablonu bulunamadı: {filePath}");

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var document = WordprocessingDocument.Open(stream, false);
            if (document.MainDocumentPart?.Document?.Body is null)
            {
                return Result.Failure<ReferenceFormatDocument>(
                    "Seçilen Word belgesinde okunabilir ana belge içeriği bulunamadı.");
            }

            return Result.Success(new ReferenceFormatDocument
            {
                FileName = Path.GetFileName(filePath),
                OriginalSourcePath = filePath,
                ResolvedFilePath = filePath,
                EmbeddedAssetEntryName = ReferenceFormatDocument.DefaultEmbeddedAssetEntryName
            });
        }
        catch (Exception)
        {
            return Result.Failure<ReferenceFormatDocument>(
                $"'{Path.GetFileName(filePath)}' geçerli bir .docx biçim şablonu olarak açılamadı.");
        }
    }
}
