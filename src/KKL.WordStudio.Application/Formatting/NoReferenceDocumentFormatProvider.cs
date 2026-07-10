namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Domain.Projects;

/// <summary>Bootstrap/direct-construction fallback. Infrastructure replaces this registration with the real DOCX provider.</summary>
public sealed class NoReferenceDocumentFormatProvider : IReferenceDocumentFormatProvider
{
    public Task<ReferenceDocumentFormatResult> ReadAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(new ReferenceDocumentFormatResult
        {
            Profile = null,
            IsMissing = false,
            StatusMessage = null
        });
    }
}
