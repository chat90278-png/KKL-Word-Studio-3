namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Preview;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint20PreviewDiagnosticFactoryTests
{
    [Fact]
    public void TableWarning_AttachesElementKeyAndOrderedExcelSourceMetadata()
    {
        const string warning = "PN/key '55' için geçerli Adet değeri bulunamadı; satırlar birleştirilmedi.";
        var tableId = Guid.NewGuid();
        var table = new TableElement
        {
            Name = "Table 1"
        };
        typeof(ReportElement).GetProperty(nameof(ReportElement.Id))!.SetValue(table, tableId);
        table.Sources.Add(new TableSourceBinding
        {
            DataSourceName = "source-1",
            WorksheetName = "SheetA",
            Range = new DataRange
            {
                HeaderRowIndex = 2,
                DataStartRow = 3,
                DataEndRow = 13,
                StartColumn = 1,
                EndColumn = 6
            }
        });

        var report = BuildReport(table);
        var project = BuildProject(report);
        var document = BuildDocument(new TableContentNode
        {
            ElementId = tableId,
            Kind = ReportContentKind.Table,
            Name = table.Name,
            ColumnHeaders = ["No", "Tr İsim", "Parça Numarası", "NSN", "Seri Numarası", "Adet"],
            Rows = [],
            CompositionWarnings = [warning],
            SourceCount = 1
        });

        var diagnostics = PreviewDiagnosticFactory.Build(project, report, document, [warning]);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(PreviewDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Adet değeri eksik veya geçersiz", diagnostic.Title);
        Assert.Equal(warning, diagnostic.Message);
        Assert.Equal(tableId, diagnostic.ElementId);
        Assert.Equal("Table 1", diagnostic.ElementName);
        Assert.Equal("55", diagnostic.KeyValue);

        var source = Assert.Single(diagnostic.Sources);
        Assert.Equal("source-1", source.DataSourceName);
        Assert.Equal(@"C:\data\source.xlsx", source.SourcePath);
        Assert.Equal("SheetA", source.WorksheetName);
        Assert.Equal("A3:F13", source.RangeReference);
    }

    [Fact]
    public void ExistingTableMessage_IsNotDuplicatedAndGenericLayoutWarningIsPreserved()
    {
        const string tableWarning = "PN/key '2354' için 'NSN' alanında çelişkili değerler var; satırlar birleştirilmedi.";
        const string layoutWarning = "İçindekiler sayfa numaraları yakınsamadi.";
        var table = new TableElement { Name = "Table 1" };
        var report = BuildReport(table);
        var project = BuildProject(report);
        var document = BuildDocument(new TableContentNode
        {
            ElementId = table.Id,
            Kind = ReportContentKind.Table,
            Name = table.Name,
            ColumnHeaders = ["Part", "NSN"],
            Rows = [],
            CompositionWarnings = [tableWarning],
            SourceCount = 0
        });

        var diagnostics = PreviewDiagnosticFactory.Build(
            project,
            report,
            document,
            [tableWarning, layoutWarning, layoutWarning]);

        Assert.Equal(2, diagnostics.Count);
        Assert.Single(diagnostics, item => item.Message == tableWarning);
        var generic = Assert.Single(diagnostics, item => item.Message == layoutWarning);
        Assert.Null(generic.ElementId);
        Assert.Empty(generic.Sources);
        Assert.Equal("Önizleme yerleşim uyarısı", generic.Title);
    }

    private static Report BuildReport(TableElement table)
    {
        var section = new Section { Name = "Body", Kind = SectionKind.Body };
        section.Root.Children.Add(table);
        var page = new Page { Name = "Page1" };
        page.Sections.Add(section);
        var report = new Report { Name = "Diagnostic report" };
        report.Pages.Add(page);
        return report;
    }

    private static Project BuildProject(Report report)
    {
        var source = new ExcelDataSource
        {
            Name = "source-1",
            Workbook = new Workbook
            {
                FileName = "source.xlsx",
                SourcePath = @"C:\data\source.xlsx"
            },
            ActiveWorksheetName = "SheetA"
        };
        source.Workbook.Worksheets.Add(new Worksheet
        {
            Name = "SheetA",
            SelectedRange = new DataRange
            {
                HeaderRowIndex = 2,
                DataStartRow = 3,
                DataEndRow = 13,
                StartColumn = 1,
                EndColumn = 6
            }
        });

        var project = new Project { Name = "Diagnostic project" };
        project.DataSources.Add(source);
        project.Reports.Add(report);
        return project;
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
            MarginTopMillimeters = 25,
            MarginBottomMillimeters = 25,
            MarginLeftMillimeters = 25,
            MarginRightMillimeters = 25,
            ShowPageNumbers = false
        }
    };
}
