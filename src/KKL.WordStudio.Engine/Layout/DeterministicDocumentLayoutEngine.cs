namespace KKL.WordStudio.Engine.Layout;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Layout;

public sealed class DeterministicDocumentLayoutEngine : IDocumentLayoutEngine
{
    private const int MaximumTocPasses = 5;
    private readonly GeneratedDocumentPaginator _generatedPaginator;
    private readonly FrontMatterPaginator _frontMatterPaginator;

    public DeterministicDocumentLayoutEngine()
    {
        var measurement = new DeterministicTextMeasurement();
        var tablePaginator = new DeterministicTablePaginator(measurement);
        _generatedPaginator = new GeneratedDocumentPaginator(measurement, tablePaginator);
        _frontMatterPaginator = new FrontMatterPaginator(measurement, tablePaginator);
    }

    public Task<DocumentLayoutResult> LayoutAsync(
        DocumentLayoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ReportContent);
        cancellationToken.ThrowIfCancellationRequested();

        var warnings = new List<string>();
        var pages = new List<DocumentPageLayout>();

        if (request.FrontMatter is not null)
        {
            foreach (var warning in request.FrontMatter.Warnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))
                AddWarningOnce(warnings, warning.Trim());
            pages.AddRange(_frontMatterPaginator.Layout(
                request.FrontMatter,
                warnings,
                cancellationToken));
        }

        foreach (var warning in request.ReportContent.FormatWarnings.Where(warning => !string.IsNullOrWhiteSpace(warning)))
            AddWarningOnce(warnings, warning.Trim());

        pages.AddRange(LayoutGeneratedWithTocPasses(
            request.ReportContent,
            pages.Count + 1,
            warnings,
            cancellationToken));

        return Task.FromResult(new DocumentLayoutResult
        {
            Pages = pages,
            Warnings = warnings
        });
    }

    private IReadOnlyList<DocumentPageLayout> LayoutGeneratedWithTocPasses(
        ReportContentDocument document,
        int firstPageNumber,
        List<string> resultWarnings,
        CancellationToken cancellationToken)
    {
        if (document.TableOfContents.Count == 0)
        {
            return _generatedPaginator.Layout(
                document,
                firstPageNumber,
                [],
                resultWarnings,
                cancellationToken);
        }

        var tocEntries = document.TableOfContents
            .Select(entry => new LaidOutTocEntry
            {
                ElementId = entry.ElementId,
                Text = entry.Text,
                Level = entry.Level,
                PageNumber = firstPageNumber
            })
            .ToList();

        IReadOnlyList<DocumentPageLayout> latestPages = [];
        IReadOnlyList<string> latestPassWarnings = [];
        var converged = false;

        for (var pass = 0; pass < MaximumTocPasses; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var passWarnings = new List<string>();
            latestPages = _generatedPaginator.Layout(
                document,
                firstPageNumber,
                tocEntries,
                passWarnings,
                cancellationToken);
            latestPassWarnings = passWarnings;

            var firstElementPages = ResolveFirstElementPageNumbers(latestPages);
            var projectedEntries = document.TableOfContents
                .Select(entry => new LaidOutTocEntry
                {
                    ElementId = entry.ElementId,
                    Text = entry.Text,
                    Level = entry.Level,
                    PageNumber = firstElementPages.TryGetValue(entry.ElementId, out var pageNumber)
                        ? pageNumber
                        : firstPageNumber
                })
                .ToList();

            if (TocPageNumbersMatch(tocEntries, projectedEntries))
            {
                converged = true;
                break;
            }

            tocEntries = projectedEntries;
        }

        if (!converged)
        {
            var finalPassWarnings = new List<string>();
            latestPages = _generatedPaginator.Layout(
                document,
                firstPageNumber,
                tocEntries,
                finalPassWarnings,
                cancellationToken);
            latestPassWarnings = finalPassWarnings;
            resultWarnings.Add($"İçindekiler sayfa numaraları {MaximumTocPasses} yerleşim geçişinde yakınsamadı; son deterministik sonuç kullanıldı.");
        }

        resultWarnings.AddRange(latestPassWarnings);
        return latestPages;
    }

    private static IReadOnlyDictionary<Guid, int> ResolveFirstElementPageNumbers(
        IReadOnlyList<DocumentPageLayout> pages)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var page in pages)
        {
            foreach (var elementId in page.Blocks
                         .Where(block => block.ElementId is not null)
                         .Select(block => block.ElementId!.Value))
            {
                result.TryAdd(elementId, page.PageNumber);
            }
        }

        return result;
    }

    private static bool TocPageNumbersMatch(
        IReadOnlyList<LaidOutTocEntry> current,
        IReadOnlyList<LaidOutTocEntry> projected) =>
        current.Count == projected.Count
        && current.Zip(projected).All(pair =>
            pair.First.ElementId == pair.Second.ElementId
            && pair.First.PageNumber == pair.Second.PageNumber);
    private static void AddWarningOnce(List<string> warnings, string warning)
    {
        if (!warnings.Any(existing => string.Equals(existing, warning, StringComparison.Ordinal)))
            warnings.Add(warning);
    }

}
