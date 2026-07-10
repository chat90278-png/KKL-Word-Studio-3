namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Editing;
using KKL.WordStudio.Application.Importing;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public class Sprint8CaptionAndDropTests
{
    private sealed class NoOpDataProviderRegistry : IDataProviderRegistry
    {
        public void Register(IDataProvider provider) { }
        public IDataProvider Resolve(string providerKey) =>
            throw new InvalidOperationException("No bound table is expected in this test.");
    }

    [Fact]
    public async Task TableCaption_IsIncludedInReportContentDocument()
    {
        var (project, report, section) = CreateProjectAndReport();
        section.Root.Children.Add(new TableElement
        {
            Name = "MotorTable",
            Caption = "Motor Özellikleri"
        });

        var builder = new ReportContentBuilder(new NoOpDataProviderRegistry());
        var document = await builder.BuildAsync(project, report);

        var tableNode = Assert.IsType<TableContentNode>(Assert.Single(document.BodyNodes));
        Assert.Equal("Motor Özellikleri", tableNode.Caption);
    }

    [Fact]
    public void UseHeadingTextAsTableCaption_CopiesTextWithoutDeletingHeading()
    {
        var (_, report, section) = CreateProjectAndReport();
        var heading = new TextElement
        {
            Name = "Heading",
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal("Uçuş Kontrol Birimi")
        };
        var table = new TableElement { Name = "ControlTable" };
        section.Root.Children.Add(heading);
        section.Root.Children.Add(table);

        var service = new ReportEditingService();
        var result = service.UseHeadingTextAsTableCaption(report, table.Id, heading.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Uçuş Kontrol Birimi", table.Caption);
        Assert.Contains(heading, section.Root.Children);
        Assert.Contains(table, section.Root.Children);
        Assert.Equal(2, section.Root.Children.Count);
        Assert.Equal("Uçuş Kontrol Birimi", heading.Content.Text);
    }

    [Fact]
    public void ExcelDropValidation_AcceptsXlsxAndRejectsUnsupportedFiles()
    {
        var xlsx = SourceFileDropValidator.EvaluateExcelDrop(new[] { @"C:\Data\Parts.xlsx" });
        var xlsm = SourceFileDropValidator.EvaluateExcelDrop(new[] { @"C:\Data\Macro.xlsm" });
        var xls = SourceFileDropValidator.EvaluateExcelDrop(new[] { @"C:\Data\Legacy.xls" });
        var pdf = SourceFileDropValidator.EvaluateExcelDrop(new[] { @"C:\Data\Report.pdf" });

        Assert.True(xlsx.IsAccepted);
        Assert.Equal(@"C:\Data\Parts.xlsx", xlsx.FilePath);
        Assert.True(xlsm.IsAccepted);
        Assert.False(xls.IsAccepted);
        Assert.False(pdf.IsAccepted);
        Assert.Contains(".xlsx", xls.Message);
        Assert.Contains(".xlsm", xls.Message);
    }

    [Fact]
    public void FrontMatterDropValidation_AcceptsDocxAndRejectsUnsupportedFiles()
    {
        var docx = SourceFileDropValidator.EvaluateFrontMatterDrop(new[] { @"C:\Data\Kapak.docx" });
        var doc = SourceFileDropValidator.EvaluateFrontMatterDrop(new[] { @"C:\Data\Legacy.doc" });
        var xlsx = SourceFileDropValidator.EvaluateFrontMatterDrop(new[] { @"C:\Data\Parts.xlsx" });

        Assert.True(docx.IsAccepted);
        Assert.Equal(@"C:\Data\Kapak.docx", docx.FilePath);
        Assert.False(doc.IsAccepted);
        Assert.False(xlsx.IsAccepted);
        Assert.Contains(".docx", doc.Message);
    }

    [Fact]
    public void MultipleExcelDrop_AcceptsFirstSupportedFileAndReportsLimitation()
    {
        var decision = SourceFileDropValidator.EvaluateExcelDrop(new[]
        {
            @"C:\Data\Ignore.txt",
            @"C:\Data\First.xlsx",
            @"C:\Data\Second.xlsm"
        });

        Assert.True(decision.IsAccepted);
        Assert.Equal(@"C:\Data\First.xlsx", decision.FilePath);
        Assert.Contains("ilk desteklenen dosya", decision.Message);
    }

    private static (Project Project, Report Report, Section Section) CreateProjectAndReport()
    {
        var project = new Project { Name = "Sprint 8 Test" };
        var report = new Report { Name = "Report" };
        var page = new Page();
        var section = new Section { Kind = SectionKind.Body };
        page.Sections.Add(section);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report, section);
    }
}
