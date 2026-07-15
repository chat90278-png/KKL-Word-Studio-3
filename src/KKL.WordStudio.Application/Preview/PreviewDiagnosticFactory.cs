namespace KKL.WordStudio.Application.Preview;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;

/// <summary>
/// Projects report-content findings into runtime diagnostics with stable code,
/// grouping and report/source navigation metadata. It does not change composition
/// or layout decisions and never repairs data automatically.
/// </summary>
public static class PreviewDiagnosticFactory
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

            foreach (var finding in tableNode.CompositionDiagnostics)
            {
                tableMessages.Add(finding.Message);
                var definition = PreviewDiagnosticCatalog.Resolve(finding.Code);
                diagnostics.Add(new PreviewDiagnostic
                {
                    Id = $"table:{tableNode.ElementId:N}:{ordinal++}",
                    Code = finding.Code,
                    GroupingKey = BuildGroupingKey(finding.Code, tableNode.ElementId, finding.AffectedColumn),
                    Severity = definition.Severity,
                    Title = definition.Title,
                    Message = finding.Message,
                    ElementId = tableNode.ElementId,
                    ElementName = tableNode.Name,
                    AffectedColumn = finding.AffectedColumn,
                    KeyValue = finding.KeyValue,
                    Sources = sources
                });
            }

            if (!string.IsNullOrWhiteSpace(tableNode.SourceError))
            {
                var message = $"'{tableNode.Name}' tablosu kaynak hatası içeriyor: {tableNode.SourceError}";
                tableMessages.Add(message);
                var definition = PreviewDiagnosticCatalog.Resolve(PreviewDiagnosticCodes.SourceAccessError);
                diagnostics.Add(new PreviewDiagnostic
                {
                    Id = $"source:{tableNode.ElementId:N}:{ordinal++}",
                    Code = PreviewDiagnosticCodes.SourceAccessError,
                    GroupingKey = BuildGroupingKey(
                        PreviewDiagnosticCodes.SourceAccessError,
                        tableNode.ElementId,
                        affectedColumn: null),
                    Severity = definition.Severity,
                    Title = definition.Title,
                    Message = message,
                    ElementId = tableNode.ElementId,
                    ElementName = tableNode.Name,
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

            var definition = PreviewDiagnosticCatalog.Resolve(PreviewDiagnosticCodes.LayoutWarning);
            diagnostics.Add(new PreviewDiagnostic
            {
                Id = $"layout:{ordinal++}",
                Code = PreviewDiagnosticCodes.LayoutWarning,
                GroupingKey = $"{PreviewDiagnosticCodes.LayoutWarning}:{NormalizeIdentity(message)}",
                Severity = definition.Severity,
                Title = definition.Title,
                Message = message
            });
        }

        return diagnostics;
    }

    private static string BuildGroupingKey(string code, Guid? elementId, string? affectedColumn) =>
        string.Join(
            ':',
            code,
            elementId?.ToString("N") ?? "global",
            NormalizeIdentity(affectedColumn));

    private static string NormalizeIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Trim().ToUpperInvariant();

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
}
