namespace KKL.WordStudio.Application.Formatting;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Styling;

public interface IReportContentFormatResolver
{
    ResolvedTextFormat ResolveText(
        DocumentFormatProfile? profile,
        ReportContentKind kind,
        Style elementStyle);

    ResolvedTableFormat ResolveTable(
        DocumentFormatProfile? profile,
        TableElement table);

    PageLayout ResolvePageLayout(
        DocumentFormatProfile? profile,
        PageLayout authoredLayout);
}
