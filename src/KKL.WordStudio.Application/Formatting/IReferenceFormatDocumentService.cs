namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Registers a user-selected DOCX as the project's reference-format asset.
/// This boundary validates only file identity/availability; DOCX parsing stays
/// behind <see cref="IReferenceDocumentFormatProvider"/> in Infrastructure.
/// </summary>
public interface IReferenceFormatDocumentService
{
    Result<ReferenceFormatDocument> Import(string filePath);
    bool IsAvailable(ReferenceFormatDocument referenceFormat);
}
