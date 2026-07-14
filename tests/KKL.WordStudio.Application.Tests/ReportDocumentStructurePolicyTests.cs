namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class ReportDocumentStructurePolicyTests
{
    private readonly ReportStructureService _service = new();

    [Fact]
    public void EnsureRootAndRenumber_MigratesLegacyReportWithoutLosingContent()
    {
        var (report, body) = CreateReport();
        var table = new TableElement { Name = "Legacy Table" };
        body.Root.Children.Add(table);

        var root = ReportDocumentStructurePolicy.EnsureRootAndRenumber(report);

        Assert.Same(root, body.Root.Children[0]);
        Assert.Same(table, body.Root.Children[1]);
        Assert.Equal("Document Root", root.Name);
        Assert.Equal("1. System Test Procedure Configuration List", root.Content.Text);
    }

    [Fact]
    public void EnsureRootAndRenumber_PreservesDuplicateLegacyRootTextAsNormalHeading()
    {
        var (report, body) = CreateReport();
        body.Root.Children.Add(Heading("Document Root", "First"));
        body.Root.Children.Add(Heading("Document Root", "Second"));

        ReportDocumentStructurePolicy.EnsureRootAndRenumber(report);

        var first = Assert.IsType<TextElement>(body.Root.Children[0]);
        var second = Assert.IsType<TextElement>(body.Root.Children[1]);
        Assert.Equal("Document Root", first.Name);
        Assert.Equal("1. First", first.Content.Text);
        Assert.Equal("Heading", second.Name);
        Assert.Equal("1.1 Second", second.Content.Text);
    }

    [Fact]
    public void Workspace_SetActiveReport_EnforcesRootBeforePublishingReport()
    {
        var workspace = new Workspace();
        var (report, body) = CreateReport();
        body.Root.Children.Add(new TableElement { Name = "Table" });

        workspace.SetActiveReport(report);

        Assert.Same(report, workspace.ActiveReport);
        Assert.True(ReportDocumentStructurePolicy.IsRoot(body.Root.Children[0]));
    }

    [Fact]
    public void Root_CannotBeDeletedMovedIndentedOrOutdented()
    {
        var (report, _) = CreateReport();
        var root = ReportDocumentStructurePolicy.EnsureRootAndRenumber(report);

        Assert.True(ReportDocumentStructurePolicy.Delete(_service, report, root.Id).IsFailure);
        Assert.True(ReportDocumentStructurePolicy.MoveUp(_service, report, root.Id).IsFailure);
        Assert.True(ReportDocumentStructurePolicy.MoveDown(_service, report, root.Id).IsFailure);
        Assert.True(ReportDocumentStructurePolicy.Indent(_service, report, root.Id).IsFailure);
        Assert.True(ReportDocumentStructurePolicy.Outdent(_service, report, root.Id).IsFailure);
    }

    [Fact]
    public void InsertHeading_WhenTableSelected_InsertsImmediatelyAboveTable()
    {
        var (report, body) = CreateReport();
        var owner = Heading("Heading", "Owner");
        var table = new TableElement { Name = "Table" };
        body.Root.Children.Add(owner);
        body.Root.Children.Add(table);
        ReportDocumentStructurePolicy.EnsureRootAndRenumber(report);

        var inserted = Heading("Heading", "Inserted");
        var result = ReportDocumentStructurePolicy.InsertHeading(report, table.Id, inserted);

        Assert.True(result.IsSuccess);
        Assert.Equal(inserted.Id, body.Root.Children[^2].Id);
        Assert.Equal(table.Id, body.Root.Children[^1].Id);
        Assert.Equal("1.2 Inserted", inserted.Content.Text);
    }

    [Fact]
    public void InsertAltHeading_WhenTableSelected_InsertsBetweenOwnerAndTable()
    {
        var (report, body) = CreateReport();
        var owner = Heading("Heading", "Owner");
        var table = new TableElement { Name = "Table" };
        body.Root.Children.Add(owner);
        body.Root.Children.Add(table);
        ReportDocumentStructurePolicy.EnsureRootAndRenumber(report);

        var inserted = Heading("Alt Heading", "Inserted", alt: true);
        var result = ReportDocumentStructurePolicy.InsertAltHeading(report, table.Id, inserted);

        Assert.True(result.IsSuccess);
        Assert.Equal(owner.Id, body.Root.Children[1].Id);
        Assert.Equal(inserted.Id, body.Root.Children[2].Id);
        Assert.Equal(table.Id, body.Root.Children[3].Id);
        Assert.True(HeadingStylePresets.IsAltHeading(inserted.Style));
        Assert.Equal("1.1.1 Inserted", inserted.Content.Text);
    }

    [Fact]
    public void InsertAltHeading_WithNoSelection_FallsBackToSafeTopLevelHeading()
    {
        var (report, body) = CreateReport();
        var inserted = Heading("Alt Heading", "Orphan", alt: true);

        var result = ReportDocumentStructurePolicy.InsertAltHeading(report, null, inserted);

        Assert.True(result.IsSuccess);
        Assert.True(HeadingStylePresets.IsHeading(inserted.Style));
        Assert.Equal("1.1 Orphan", inserted.Content.Text);
        Assert.Equal(inserted.Id, body.Root.Children[1].Id);
    }

    [Fact]
    public void MoveDown_ReordersOnlySiblingHeadingBlockAndRenumbers()
    {
        var (report, body) = CreateReport();
        var first = Heading("Heading", "First");
        var firstTable = new TableElement { Name = "First Table" };
        var second = Heading("Heading", "Second");
        var secondTable = new TableElement { Name = "Second Table" };
        body.Root.Children.AddRange([first, firstTable, second, secondTable]);
        ReportDocumentStructurePolicy.EnsureRootAndRenumber(report);

        var result = ReportDocumentStructurePolicy.MoveDown(_service, report, first.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(second.Id, body.Root.Children[1].Id);
        Assert.Equal(secondTable.Id, body.Root.Children[2].Id);
        Assert.Equal(first.Id, body.Root.Children[3].Id);
        Assert.Equal(firstTable.Id, body.Root.Children[4].Id);
        Assert.Equal("1.1 Second", second.Content.Text);
        Assert.Equal("1.2 First", first.Content.Text);
    }

    [Fact]
    public void Rename_ReplacesVisibleNumberWithoutDuplicatingPrefix()
    {
        var (report, body) = CreateReport();
        var heading = Heading("Heading", "Old");
        body.Root.Children.Add(heading);
        ReportDocumentStructurePolicy.EnsureRootAndRenumber(report);

        var result = ReportDocumentStructurePolicy.Rename(_service, report, heading.Id, "9.8 New title");

        Assert.True(result.IsSuccess);
        Assert.Equal("1.1 New title", heading.Content.Text);
    }

    [Fact]
    public void MoveIntoRoot_PromotesAltHeadingToTopLevelAfterSuccessfulMove()
    {
        var (report, body) = CreateReport();
        var owner = Heading("Heading", "Owner");
        var alt = Heading("Alt Heading", "Nested", alt: true);
        body.Root.Children.Add(owner);
        body.Root.Children.Add(alt);
        var root = ReportDocumentStructurePolicy.EnsureRootAndRenumber(report);

        var result = ReportDocumentStructurePolicy.Move(
            _service,
            report,
            alt.Id,
            root.Id,
            StructureDropMode.Into);

        Assert.True(result.IsSuccess);
        Assert.Equal(alt.Id, body.Root.Children[1].Id);
        Assert.True(HeadingStylePresets.IsHeading(alt.Style));
        Assert.Equal("1.1 Nested", alt.Content.Text);
    }

    private static (Report Report, Section Body) CreateReport()
    {
        var report = new Report { Name = "Report" };
        var page = new Page();
        var body = new Section { Name = "Body", Kind = SectionKind.Body, AutoHeight = true };
        page.Sections.Add(body);
        report.Pages.Add(page);
        return (report, body);
    }

    private static TextElement Heading(string name, string text, bool alt = false) => new()
    {
        Name = name,
        Style = alt ? HeadingStylePresets.CreateAltHeadingStyle() : HeadingStylePresets.CreateHeadingStyle(),
        Content = Expression.Literal(text)
    };
}
