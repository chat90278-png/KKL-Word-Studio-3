namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Domain.Projects;

public interface IReferenceDocumentFormatProvider
{
    Task<ReferenceDocumentFormatResult> ReadAsync(
        Project project,
        CancellationToken cancellationToken = default);
}
