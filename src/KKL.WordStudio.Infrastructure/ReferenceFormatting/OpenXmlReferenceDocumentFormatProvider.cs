namespace KKL.WordStudio.Infrastructure.ReferenceFormatting;

using DocumentFormat.OpenXml.Packaging;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Domain.Projects;

/// <summary>
/// Reads the project-owned reference DOCX in read-only mode and projects only
/// the supported Sprint 16 WordprocessingML properties into the frozen
/// Application format contract. When no usable project reference is available,
/// the deterministic built-in default profile is returned so all shared format
/// metadata, including caption format and sequence semantics, stays complete.
/// </summary>
public sealed class OpenXmlReferenceDocumentFormatProvider : IReferenceDocumentFormatProvider
{
    private readonly OpenXmlReferenceFormatAnalyzer _analyzer = new();

    public Task<ReferenceDocumentFormatResult> ReadAsync(
        Project project,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var referenceFormat = project.ReferenceFormat;
        if (referenceFormat is null)
        {
            return Task.FromResult(new ReferenceDocumentFormatResult
            {
                Profile = DefaultDocumentFormatProfileFactory.Create(),
                IsMissing = false,
                StatusMessage = null
            });
        }

        var sourcePath = ReferenceFormatSourcePathResolver.Resolve(referenceFormat);
        if (sourcePath is null)
        {
            return Task.FromResult(new ReferenceDocumentFormatResult
            {
                Profile = DefaultDocumentFormatProfileFactory.Create(),
                IsMissing = true,
                StatusMessage = $"Biçim şablonu bulunamadı: {referenceFormat.FileName}. Varsayılan KKL belge biçimi kullanılacak."
            });
        }

        try
        {
            using var document = WordprocessingDocument.Open(sourcePath, false);
            if (document.MainDocumentPart?.Document?.Body is null)
            {
                return Task.FromResult(new ReferenceDocumentFormatResult
                {
                    Profile = DefaultDocumentFormatProfileFactory.Create(),
                    IsMissing = true,
                    StatusMessage = $"Biçim şablonu okunamadı: {referenceFormat.FileName}. Varsayılan KKL belge biçimi kullanılacak."
                });
            }

            var profile = _analyzer.Analyze(document, cancellationToken);
            return Task.FromResult(new ReferenceDocumentFormatResult
            {
                Profile = profile,
                IsMissing = false,
                StatusMessage = $"Biçim şablonu yüklendi: {referenceFormat.FileName}"
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Task.FromResult(new ReferenceDocumentFormatResult
            {
                Profile = DefaultDocumentFormatProfileFactory.Create(),
                IsMissing = true,
                StatusMessage = $"Biçim şablonu okunamadı: {referenceFormat.FileName}. Varsayılan KKL belge biçimi kullanılacak."
            });
        }
    }
}
