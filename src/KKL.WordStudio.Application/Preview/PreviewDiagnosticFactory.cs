namespace KKL.WordStudio.Application.Preview;

using System.Text.RegularExpressions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;

/// <summary>
/// Projects existing report-content warnings into runtime diagnostics with
/// stable codes and report/source navigation metadata. It never repairs data
/// automatically and does not introduce a second validation pipeline.
/// </summary>
public static partial class PreviewDiagnosticFactory
{
    public static IReadOnlyList<PreviewDiagnostic> Build(
        Project project,
        Report report,
        ReportContentDocument document,
        IReadOnlyList<string> layoutWarnings)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layoutWarnings);

        var tableElements = EnumerateReportElements(report)
            .OfType<TableElement>()
            .ToDictionary(table => table.Id);
        var diagnostics = new List<PreviewDiagnostic>();
        var tableMessages = new HashSet<string>(StringComparer.Ordinal);
        var ordinal = 0;

        foreach (var tableNode in document.BodyNodes.OfType<TableContentNode>())
        {
            tableElements.TryGetValue(tableNode.ElementId, out var tableElement);
            var sources = tableElement is null
                ? Array.Empty<PreviewDiagnosticSource>()
                : BuildSources(project, tableElement);

            foreach (var warning in tableNode.CompositionWarnings.Where(message => !string.IsNullOrWhiteSpace(message)))
            {
                var message = warning.Trim();
                var rule = PreviewDiagnosticCatalog.ResolveComposition(message);
                tableMessages.Add(message);
                diagnostics.Add(new PreviewDiagnostic
                {
                    Id = $"table:{tableNode.ElementId:N}:{ordinal++}",
                    Code = rule.Code,
                    Severity = rule.Severity,
                    Title = rule.Title,
                    Message = message,
                    ElementId = tableNode.ElementId,
                    ElementName = tableNode.Name,
                    AffectedColumn = rule.AffectedColumn,
                    RowNumber = TryExtractRowNumber(message),
                    KeyValue = TryExtractKey(message),
                    Sources = sources
                });
            }

            if (!string.IsNullOrWhiteSpace(tableNode.SourceError))
            {
                var sourceError = tableNode.SourceError.Trim();
                var rule = PreviewDiagnosticCatalog.ResolveSourceError(sourceError);
                var message = $"'{tableNode.Name}' tablosu kaynak hatası içeriyor: {sourceError}";
                tableMessages.Add(message);
                diagnostics.Add(new PreviewDiagnostic
                {
                    Id = $"source:{tableNode.ElementId:N}:{ordinal++}",
                    Code = rule.Code,
                    Severity = rule.Severity,
                    Title = rule.Title,
                    Message = message,
                    ElementId = tableNode.ElementId,
                    ElementName = tableNode.Name,
                    AffectedColumn = rule.AffectedColumn,
                    Sources = sources
                });
            }
        }

        foreach (var warning in layoutWarnings.Where(message => !string.IsNullOrWhiteSpace(message)))
        {
            var message = warning.Trim();
            if (tableMessages.Contains(message)
                || diagnostics.Any(item => string.Equals(item.Message, message, StringComparison.Ordinal)))
            {
                continue;
            }

            var rule = PreviewDiagnosticCatalog.ResolveLayout(message);
            diagnostics.Add(new PreviewDiagnostic
            {
                Id = $"layout:{ordinal++}",
                Code = rule.Code,
                Severity = rule.Severity,
                Title = rule.Title,
                Message = message,
                AffectedColumn = rule.AffectedColumn
            });
        }

        return diagnostics;
    }

    private static IReadOnlyList<PreviewDiagnosticSource> BuildSources(Project project, TableElement table)
    {
        var result = new List<PreviewDiagnosticSource>();
        var keyColumnId = table.SerialQuantityGrouping?.MatchKeyColumnId;

        if (table.Sources.Count > 0)
        {
            foreach (var binding in table.Sources)
            {
                var keyField = keyColumnId is { } columnId
                    ? binding.FieldMappings.FirstOrDefault(mapping => mapping.TableColumnId == columnId)?.SourceField
                    : null;
                AddSource(result, project, binding.DataSourceName, binding.WorksheetName, binding.Range.RangeReference, keyField);
            }

            return result;
        }

        if (table.Binding is { } legacyBinding)
        {
            var keyField = keyColumnId is { } columnId
                ? table.Columns.FirstOrDefault(column => column.Id == columnId)?.SourceField
                : null;
            AddSource(result, project, legacyBinding.DataSourceName, legacyBinding.WorksheetName, rangeReference: null, keyField);
        }

        return result;
    }

    private static void AddSource(
        ICollection<PreviewDiagnosticSource> result,
        Project project,
        string dataSourceName,
        string? requestedWorksheetName,
        string? rangeReference,
        string? keyColumnIdentity)
    {
        var source = project.DataSources
            .OfType<ExcelDataSource>()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, dataSourceName, StringComparison.Ordinal));
        var worksheetName = requestedWorksheetName ?? source?.ActiveWorksheetName;
        var worksheet = source?.Workbook.Worksheets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, worksheetName, StringComparison.Ordinal));

        result.Add(new PreviewDiagnosticSource
        {
            DataSourceName = dataSourceName,
            SourcePath = source?.Workbook.SourcePath,
            WorksheetName = worksheetName,
            RangeReference = rangeReference ?? worksheet?.SelectedRange?.RangeReference,
            KeyColumnIdentity = keyColumnIdentity
        });
    }

    private static IEnumerable<ReportElement> EnumerateReportElements(Report report)
    {
        foreach (var section in report.Pages.SelectMany(page => page.Sections))
        {
            foreach (var element in Enumerate(section.Root))
                yield return element;
        }
    }

    private static IEnumerable<ReportElement> Enumerate(Container container)
    {
        foreach (var child in container.Children)
        {
            yield return child;
            if (child is Container nested)
            {
                foreach (var descendant in Enumerate(nested))
                    yield return descendant;
            }
        }
    }

    private static string? TryExtractKey(string message)
    {
        var match = PartNumberKeyRegex().Match(message);
        return match.Success ? match.Groups["key"].Value : null;
    }

    private static int? TryExtractRowNumber(string message)
    {
        var match = RowNumberRegex().Match(message);
        return match.Success && int.TryParse(match.Groups["row"].Value, out var row)
            ? row
            : null;
    }

    [GeneratedRegex("PN/key\\s+'(?<key>[^']+)'", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PartNumberKeyRegex();

    [GeneratedRegex("(?:satır|row)\\s*[:#-]?\\s*(?<row>\\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RowNumberRegex();
}
