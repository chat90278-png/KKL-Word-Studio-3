namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint24StructuredDiagnosticsTests
{
    [Fact]
    public void CompositionClassifier_EmitsStableCodeKeyAndAffectedColumn()
    {
        const string warning = "PN/key 'P-42' için 'NSN' alanında çelişkili değerler var; satırlar birleştirilmedi.";

        var diagnostic = Assert.Single(TableCompositionDiagnosticClassifier.Classify([warning]));

        Assert.Equal(TableCompositionDiagnosticCodes.MergeConflict, diagnostic.Code);
        Assert.Equal(warning, diagnostic.Message);
        Assert.Equal("P-42", diagnostic.KeyValue);
        Assert.Equal("NSN", diagnostic.AffectedColumn);
    }

    [Fact]
    public void CompositionProjection_PreservesFrozenResultAndKeepsLegacyMessages()
    {
        const string warning = "PN/key '55' için geçerli Adet değeri bulunamadı; satırlar birleştirilmedi.";
        var result = new TableRowCompositionResult
        {
            Rows = [],
            CellSpans = [],
            RowGroups = [],
            Warnings = [warning]
        };

        Assert.Equal(warning, Assert.Single(result.Warnings));
        var diagnostic = Assert.Single(TableCompositionDiagnosticClassifier.Classify(result.Warnings));
        Assert.Equal(TableCompositionDiagnosticCodes.QuantityMissing, diagnostic.Code);
        Assert.Equal("55", diagnostic.KeyValue);
        Assert.Equal("Adet", diagnostic.AffectedColumn);
        Assert.Null(typeof(TableRowCompositionResult).GetProperty("Diagnostics"));
    }

    [Fact]
    public void Factory_UsesStructuredIdentityForCompositionFinding()
    {
        const string warning = "PN/key '55' için geçerli Adet değeri bulunamadı; satırlar birleştirilmedi.";
        var table = new TableElement { Name = "Table 1" };
        var report = BuildReport(table);
        var project = new Project { Name = "Structured diagnostics" };
        project.Reports.Add(report);
        var document = BuildDocument(new TableContentNode
        {
            ElementId = table.Id,
            Kind = ReportContentKind.Table,
            Name = table.Name,
            ColumnHeaders = ["PN", "Adet"],
            Rows = [],
            CompositionWarnings = [warning]
        });

        var diagnostic = Assert.Single(PreviewDiagnosticFactory.Build(project, report, document, [warning]));

        Assert.Equal(TableCompositionDiagnosticCodes.QuantityMissing, diagnostic.Code);
        Assert.Contains(TableCompositionDiagnosticCodes.QuantityMissing, diagnostic.GroupingKey, StringComparison.Ordinal);
        Assert.Contains(table.Id.ToString("N"), diagnostic.GroupingKey, StringComparison.Ordinal);
        Assert.Equal("Adet değeri eksik veya geçersiz", diagnostic.Title);
        Assert.Equal("55", diagnostic.KeyValue);
        Assert.Equal("Adet", diagnostic.AffectedColumn);
    }

    [Fact]
    public void Summary_GroupsLocalizedMessagesByFactoryOwnedSemanticIdentity()
    {
        var elementId = Guid.NewGuid();
        const string groupingKey = "TABLE_QUANTITY_MISSING:table:ADET";
        var diagnostics = new[]
        {
            StructuredDiagnostic("d1", elementId, "A-1", groupingKey,
                "PN/key 'A-1' için geçerli Adet değeri bulunamadı; satırlar birleştirilmedi."),
            StructuredDiagnostic("d2", elementId, "A-2", groupingKey,
                "Quantity is missing for PN/key 'A-2'; rows were left separate.")
        };

        var group = Assert.Single(PreviewDiagnosticSummaryService.Group(diagnostics));

        Assert.Equal(TableCompositionDiagnosticCodes.QuantityMissing, group.Code);
        Assert.Equal(2, group.OccurrenceCount);
        Assert.Equal(2, group.DistinctKeyCount);
        Assert.Equal(new[] { "A-1", "A-2" }, group.KeyValues);
    }

    [Fact]
    public void Summary_PreservesTrueKeyCountBeyondNavigationWindow()
    {
        var elementId = Guid.NewGuid();
        const string groupingKey = "TABLE_QUANTITY_MISSING:table:ADET";
        var diagnostics = Enumerable.Range(1, 30)
            .Select(index => StructuredDiagnostic(
                $"d{index}",
                elementId,
                $"K-{index}",
                groupingKey,
                $"PN/key 'K-{index}' için geçerli Adet değeri bulunamadı; satırlar birleştirilmedi."))
            .ToList();

        var group = Assert.Single(PreviewDiagnosticSummaryService.Group(diagnostics));

        Assert.Equal(30, group.OccurrenceCount);
        Assert.Equal(30, group.DistinctKeyCount);
        Assert.Equal(25, group.KeyValues.Count);
    }

    [Fact]
    public void Summary_DoesNotMergeDifferentCodesOnSameTable()
    {
        var elementId = Guid.NewGuid();
        var diagnostics = new[]
        {
            StructuredDiagnostic(
                "quantity",
                elementId,
                "A-1",
                $"{TableCompositionDiagnosticCodes.QuantityMissing}:{elementId:N}:ADET",
                "Missing quantity."),
            new PreviewDiagnostic
            {
                Id = "serial",
                Code = TableCompositionDiagnosticCodes.SerialDuplicate,
                GroupingKey = $"{TableCompositionDiagnosticCodes.SerialDuplicate}:{elementId:N}:SERI",
                Severity = PreviewDiagnosticSeverity.Warning,
                Title = "Seri numarası tekrarı",
                Message = "Duplicate serial.",
                ElementId = elementId,
                ElementName = "Table 1",
                AffectedColumn = "Seri No",
                KeyValue = "A-1"
            }
        };

        Assert.Equal(2, PreviewDiagnosticSummaryService.Group(diagnostics).Count);
    }

    [Fact]
    public void Factory_DoesNotMergeDifferentUnknownLegacyWarningsOnSameTable()
    {
        const string firstWarning = "İlk tanımlanamayan tablo uyarısı.";
        const string secondWarning = "İkinci tanımlanamayan tablo uyarısı.";
        var table = new TableElement { Name = "Table 1" };
        var report = BuildReport(table);
        var project = new Project { Name = "Legacy warning separation" };
        project.Reports.Add(report);
        var document = BuildDocument(new TableContentNode
        {
            ElementId = table.Id,
            Kind = ReportContentKind.Table,
            Name = table.Name,
            ColumnHeaders = ["A"],
            Rows = [],
            CompositionWarnings = [firstWarning, secondWarning]
        });

        var diagnostics = PreviewDiagnosticFactory.Build(
            project,
            report,
            document,
            [firstWarning, secondWarning]);

        Assert.Equal(2, diagnostics.Count);
        Assert.All(diagnostics, diagnostic =>
            Assert.Equal(TableCompositionDiagnosticCodes.LegacyWarning, diagnostic.Code));
        Assert.NotEqual(diagnostics[0].GroupingKey, diagnostics[1].GroupingKey);
        Assert.Equal(2, PreviewDiagnosticSummaryService.Group(diagnostics).Count);
    }

    private static PreviewDiagnostic StructuredDiagnostic(
        string id,
        Guid elementId,
        string key,
        string groupingKey,
        string message) => new()
    {
        Id = id,
        Code = TableCompositionDiagnosticCodes.QuantityMissing,
        GroupingKey = groupingKey,
        Severity = PreviewDiagnosticSeverity.Warning,
        Title = "Adet değeri eksik veya geçersiz",
        Message = message,
        ElementId = elementId,
        ElementName = "Table 1",
        AffectedColumn = "Adet",
        KeyValue = key
    };

    private static Report BuildReport(TableElement table)
    {
        var section = new Section { Name = "Body", Kind = SectionKind.Body };
        section.Root.Children.Add(table);
        var page = new Page { Name = "Page1" };
        page.Sections.Add(section);
        var report = new Report { Name = "Report" };
        report.Pages.Add(page);
        return report;
    }

    private static ReportContentDocument BuildDocument(TableContentNode table) => new()
    {
        HeaderNodes = [],
        BodyNodes = [table],
        FooterNodes = [],
        TableOfContents = [],
        PageLayout = new PageLayout
        {
            WidthMillimeters = 210,
            HeightMillimeters = 297,
            MarginTopMillimeters = 20,
            MarginBottomMillimeters = 20,
            MarginLeftMillimeters = 20,
            MarginRightMillimeters = 20,
            ShowPageNumbers = false
        }
    };
}
