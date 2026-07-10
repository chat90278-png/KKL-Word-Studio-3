namespace KKL.WordStudio.Application.Abstractions;

using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Shared.Results;

/// <summary>
/// Validates/registers a Word cover/preface source and exposes its current
/// availability. The implementation is Infrastructure-owned because DOCX
/// package and file-system validation depend on OpenXML/I/O.
/// </summary>
public interface IFrontMatterDocumentService
{
    Result<FrontMatterDocument> Import(string filePath);
    bool IsAvailable(FrontMatterDocument frontMatter);
}
