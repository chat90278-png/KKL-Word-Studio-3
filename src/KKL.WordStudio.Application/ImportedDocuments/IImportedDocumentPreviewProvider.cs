namespace KKL.WordStudio.Application.ImportedDocuments;

using KKL.WordStudio.Domain.Projects;

public interface IImportedDocumentPreviewProvider
{
    Task<ImportedDocumentPreviewResult> ReadAsync(
        Project project,
        CancellationToken cancellationToken = default);
}
