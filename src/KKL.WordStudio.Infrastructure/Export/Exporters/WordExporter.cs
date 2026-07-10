namespace KKL.WordStudio.Infrastructure.Export.Exporters;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.Export.Exporters.Word;
using KKL.WordStudio.Infrastructure.Export.Composition;
using KKL.WordStudio.Infrastructure.Word;
using KKL.WordStudio.Shared.Results;
using Microsoft.Extensions.Logging;

/// <summary>
/// Translates the abstract Report model into a .docx stream using the
/// OpenXML SDK. Deliberately thin: this class only orchestrates —
/// building the shared content document, then delegating each concern
/// (styles, header/footer, page layout, paragraphs, tables) to a small,
/// single-purpose writer class under Word/. Split in Sprint 6 purely for
/// readability as the class grew; none of the writers are behind an
/// interface, since there's exactly one way each needs to work today —
/// introducing Strategy/DI-swappable writers now would be unearned
/// abstraction (see ADR 0008).
/// </summary>
public sealed class WordExporter : IReportExporter
{
    private readonly IReportContentBuilder _contentBuilder;
    private readonly ILogger<WordExporter> _logger;

    public WordExporter(IReportContentBuilder contentBuilder, ILogger<WordExporter> logger)
    {
        _contentBuilder = contentBuilder;
        _logger = logger;
    }

    public string FormatKey => "docx";
    public string DisplayName => "Word Document (.docx)";

    public async Task<Result<Stream>> ExportAsync(Project project, Report report, ExportOptions options, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _contentBuilder.BuildAsync(project, report, cancellationToken);
            var sourceError = document.HeaderNodes.Concat(document.BodyNodes).Concat(document.FooterNodes)
                .OfType<TableContentNode>()
                .FirstOrDefault(table => !string.IsNullOrWhiteSpace(table.SourceError))?.SourceError;
            if (sourceError is not null)
                return Result.Failure<Stream>($"Word dosyası oluşturulamadı. {sourceError}");

            var stream = new MemoryStream();
            using (var wordDocument = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, autoSave: false))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                WordStyleWriter.AddStyleDefinitions(mainPart);

                var frontMatterPath = FrontMatterSourcePathResolver.Resolve(project.FrontMatter);
                if (frontMatterPath is not null)
                {
                    WordFrontMatterComposer.AppendFrontMatter(mainPart, body, frontMatterPath);
                }
                else if (project.FrontMatter is not null)
                {
                    _logger.LogWarning(
                        "Front-matter asset {FileName} is missing; exporting generated report content without it",
                        project.FrontMatter.FileName);
                }

                if (report.IncludeTableOfContents && document.TableOfContents.Count > 0)
                    body.AppendChild(WordParagraphWriter.BuildTocParagraph());

                var captionSequenceCounters = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var node in document.BodyNodes)
                    WordContentWriter.AppendNode(body, node, captionSequenceCounters);

                var sectionProperties = new SectionProperties();
                WordHeaderFooterWriter.AppendHeaderFooterReferences(mainPart, sectionProperties, document);
                WordPageLayoutWriter.AppendPageLayout(sectionProperties, document.PageLayout);
                body.AppendChild(sectionProperties);

                mainPart.Document.Save();
            }

            stream.Position = 0;
            return Result.Success<Stream>(stream);
        }
        catch (Exception ex)
        {
            // Technical detail goes to the log; the user gets an actionable,
            // non-technical message (Sprint 6: user-friendly error messages).
            _logger.LogError(ex, "Failed to export report {ReportId} to Word", report.Id);
            return Result.Failure<Stream>(
                "Word dosyası oluşturulamadı. Bağlı tabloların veri kaynaklarının hâlâ erişilebilir olduğundan emin olup yeniden deneyin.");
        }
    }
}
