namespace KKL.WordStudio.UI.Preview;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.ImportedDocuments;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;

/// <summary>
/// Preview orchestration boundary. Generated report semantics still come from
/// IReportContentBuilder; imported front matter is read through its Application
/// contract; pagination/layout is delegated to IDocumentLayoutEngine.
/// Compatibility PreviewBlock lists remain until Team B switches the UI to
/// PreviewSnapshot.Layout.Pages.
/// </summary>
public sealed class PreviewRenderer : IReportPreviewRenderer
{
    private readonly IReportContentBuilder _contentBuilder;
    private readonly IDocumentLayoutEngine _layoutEngine;
    private readonly IImportedDocumentPreviewProvider _importedDocumentPreviewProvider;

    public PreviewRenderer(
        IReportContentBuilder contentBuilder,
        IDocumentLayoutEngine layoutEngine,
        IImportedDocumentPreviewProvider importedDocumentPreviewProvider)
    {
        _contentBuilder = contentBuilder;
        _layoutEngine = layoutEngine;
        _importedDocumentPreviewProvider = importedDocumentPreviewProvider;
    }

    public async Task<PreviewSnapshot> RenderAsync(
        Project project,
        Report report,
        CancellationToken cancellationToken = default)
    {
        var document = await _contentBuilder.BuildAsync(project, report, cancellationToken);
        var frontMatter = await _importedDocumentPreviewProvider.ReadAsync(project, cancellationToken);
        var layout = await _layoutEngine.LayoutAsync(
            new DocumentLayoutRequest
            {
                ReportContent = document,
                FrontMatter = frontMatter.Document
            },
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(frontMatter.StatusMessage))
        {
            var mergedWarnings = layout.Warnings
                .Append(frontMatter.StatusMessage)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            layout = new DocumentLayoutResult
            {
                Pages = layout.Pages,
                Warnings = mergedWarnings
            };
        }

        return new PreviewSnapshot
        {
            HeaderBlocks = BuildBlocks(document.HeaderNodes),
            BodyBlocks = BuildBlocks(document.BodyNodes),
            FooterBlocks = BuildBlocks(document.FooterNodes),
            TableOfContents = document.TableOfContents,
            PageLayout = document.PageLayout,
            Layout = layout
        };
    }

    private static List<PreviewBlock> BuildBlocks(IReadOnlyList<ReportContentNode> nodes)
    {
        var blocks = new List<PreviewBlock>();

        foreach (var node in nodes)
        {
            PreviewBlock? block = node switch
            {
                TextContentNode text => new TextPreviewBlock { ElementId = text.ElementId, Kind = text.Kind, Text = text.Text },
                TableContentNode table => new TablePreviewBlock
                {
                    ElementId = table.ElementId,
                    Name = table.Name,
                    Caption = table.Caption,
                    ColumnHeaders = table.ColumnHeaders,
                    Rows = table.Rows,
                    DataSourceName = table.DataSourceName,
                    SourceCount = table.SourceCount,
                    SourceError = table.SourceError,
                    FilterWasIgnored = table.FilterWasIgnored
                },
                ImageContentNode image => new TextPreviewBlock
                {
                    ElementId = image.ElementId,
                    Kind = ReportContentKind.Image,
                    Text = $"[Image: {image.Name}]"
                },
                _ => null
            };

            if (block is not null)
                blocks.Add(block);
        }

        return blocks;
    }
}
