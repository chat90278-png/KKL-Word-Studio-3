namespace KKL.WordStudio.Application.Layout;

public interface IDocumentLayoutEngine
{
    Task<DocumentLayoutResult> LayoutAsync(
        DocumentLayoutRequest request,
        CancellationToken cancellationToken = default);
}
