namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public class Sprint12ReportStructureTests
{
    // ---------------------------------------------------------------
    // Rename
    // ---------------------------------------------------------------

    [Fact]
    public void RenameHeading_UpdatesSemanticContent()
    {
        var (report, body) = NewReport();
        var heading = Heading("Eski Başlık");
        body.Children.Add(heading);
        var service = new ReportStructureService();

        AssertOk(service.Rename(report, heading.Id, "Yeni Başlık"));

        Assert.Equal("Yeni Başlık", heading.Content.Text);
    }

    [Fact]
    public void RenameTable_ChangesNameWithoutChangingCaptionOrBinding()
    {
        var (report, body) = NewReport();
        var table = new TableElement { Name = "Tablo 1", Caption = "Orijinal Başlık" };
        table.Columns.Add(new TableColumn { Header = "H1", SourceField = "F1" });
        table.Binding = new Domain.DataBinding.Binding { DataSourceName = "DS1", WorksheetName = "Sheet1" };
        body.Children.Add(table);
        var service = new ReportStructureService();

        AssertOk(service.Rename(report, table.Id, "Yeni Tablo Adı"));

        Assert.Equal("Yeni Tablo Adı", table.Name);
        Assert.Equal("Orijinal Başlık", table.Caption);
        Assert.Equal("H1", table.Columns[0].Header);
        Assert.Equal("F1", table.Columns[0].SourceField);
        Assert.Equal("DS1", table.Binding!.DataSourceName);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    [Fact]
    public void DeleteTable_DoesNotDeleteProjectDataSources()
    {
        var (report, body) = NewReport();
        var project = new Project();
        project.Reports.Add(report);
        var dataSource = new ExcelDataSource
        {
            Name = "DS1",
            Workbook = new Workbook { FileName = "b.xlsx", SourcePath = "/tmp/b.xlsx" }
        };
        project.DataSources.Add(dataSource);
        var table = new TableElement { Name = "Tablo 1", Binding = new Domain.DataBinding.Binding { DataSourceName = "DS1" } };
        body.Children.Add(table);
        var service = new ReportStructureService();

        AssertOk(service.Delete(report, table.Id));

        Assert.Empty(body.Children);
        Assert.Single(project.DataSources);
        Assert.Same(dataSource, project.DataSources[0]);
    }

    [Fact]
    public void DeleteHeading_DoesNotCascadeDeleteProjectedChildren()
    {
        var (report, body) = NewReport();
        var h1 = Heading("Bölüm 1");
        var table = new TableElement { Name = "Tablo 1", Caption = "Tablo başlığı" };
        var h2 = AltHeading("Alt Bölüm");
        body.Children.Add(h1);
        body.Children.Add(table);
        body.Children.Add(h2);
        var service = new ReportStructureService();

        AssertOk(service.Delete(report, h1.Id));

        // Only the heading is gone; its projected children remain in order,
        // and the table caption (a value copy) is untouched.
        Assert.Equal(new ReportElement[] { table, h2 }, body.Children);
        Assert.Equal("Tablo başlığı", table.Caption);
    }

    // ---------------------------------------------------------------
    // Move up / down — logical blocks
    // ---------------------------------------------------------------

    [Fact]
    public void MoveHeading1_MovesEntireLogicalSubtree()
    {
        var (report, body) = NewReport();
        // Block A: H1(A) + table + H2 + table ; Block B: H1(B) + table
        var a = Heading("A");
        var aTable = Table("A-tab");
        var aSub = AltHeading("A-sub");
        var aSubTable = Table("A-sub-tab");
        var b = Heading("B");
        var bTable = Table("B-tab");
        foreach (var e in new ReportElement[] { a, aTable, aSub, aSubTable, b, bTable })
            body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.MoveDown(report, a.Id));

        // The entire A subtree moves as one block after the B block.
        Assert.Equal(
            new ReportElement[] { b, bTable, a, aTable, aSub, aSubTable },
            body.Children);
    }

    [Fact]
    public void MoveHeading2_MovesHeadingAndFollowingTablesAsBlock()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var sub1 = AltHeading("Sub1");
        var sub1Table = Table("Sub1-tab");
        var sub2 = AltHeading("Sub2");
        var sub2Table = Table("Sub2-tab");
        foreach (var e in new ReportElement[] { h1, sub1, sub1Table, sub2, sub2Table })
            body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.MoveDown(report, sub1.Id));

        // Sub1 + its table move as one block past Sub2 + its table.
        Assert.Equal(
            new ReportElement[] { h1, sub2, sub2Table, sub1, sub1Table },
            body.Children);
    }

    [Fact]
    public void MoveTable_ChangesDocumentOrder()
    {
        var (report, body) = NewReport();
        var t1 = Table("T1");
        var t2 = Table("T2");
        body.Children.Add(t1);
        body.Children.Add(t2);
        var service = new ReportStructureService();

        AssertOk(service.MoveDown(report, t1.Id));
        Assert.Equal(new ReportElement[] { t2, t1 }, body.Children);

        AssertOk(service.MoveUp(report, t1.Id));
        Assert.Equal(new ReportElement[] { t1, t2 }, body.Children);
    }

    [Fact]
    public void MoveUp_AtTop_FailsWithoutMutation()
    {
        var (report, body) = NewReport();
        var t1 = Table("T1");
        var t2 = Table("T2");
        body.Children.Add(t1);
        body.Children.Add(t2);
        var service = new ReportStructureService();

        var result = service.MoveUp(report, t1.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(new ReportElement[] { t1, t2 }, body.Children);
    }

    // ---------------------------------------------------------------
    // Indent / outdent
    // ---------------------------------------------------------------

    [Fact]
    public void IndentHeading1_ChangesItToHeading2()
    {
        var (report, body) = NewReport();
        var h = Heading("H");
        body.Children.Add(h);
        var service = new ReportStructureService();

        AssertOk(service.Indent(report, h.Id));

        Assert.True(HeadingStylePresets.IsAltHeading(h.Style));
        Assert.False(HeadingStylePresets.IsHeading(h.Style));
    }

    [Fact]
    public void OutdentHeading2_ChangesItToHeading1()
    {
        var (report, body) = NewReport();
        var h = AltHeading("H2");
        body.Children.Add(h);
        var service = new ReportStructureService();

        AssertOk(service.Outdent(report, h.Id));

        Assert.True(HeadingStylePresets.IsHeading(h.Style));
    }

    [Fact]
    public void IndentHeading2_IsRejected()
    {
        var (report, body) = NewReport();
        var h = AltHeading("H2");
        body.Children.Add(h);
        var service = new ReportStructureService();

        Assert.True(service.Indent(report, h.Id).IsFailure);
        Assert.True(HeadingStylePresets.IsAltHeading(h.Style));
    }

    // ---------------------------------------------------------------
    // Drag & drop
    // ---------------------------------------------------------------

    [Fact]
    public void DragTableIntoHeading_MovesTableIntoDerivedHeadingScope()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var h1Table = Table("H1-tab");
        var h2 = Heading("H2");
        var looseTable = Table("Loose");
        foreach (var e in new ReportElement[] { h1, h1Table, h2, looseTable })
            body.Children.Add(e);
        var service = new ReportStructureService();

        // Drop the loose table Into H1: it should append at end of H1's scope,
        // i.e. right after h1Table and before h2.
        AssertOk(service.Move(report, looseTable.Id, h1.Id, StructureDropMode.Into));

        Assert.Equal(
            new ReportElement[] { h1, h1Table, looseTable, h2 },
            body.Children);
    }

    [Fact]
    public void DragHeading1IntoHeading1_BecomesHeading2()
    {
        var (report, body) = NewReport();
        var h1a = Heading("A");
        var h1b = Heading("B");
        var bTable = Table("B-tab");
        foreach (var e in new ReportElement[] { h1a, h1b, bTable })
            body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.Move(report, h1a.Id, h1b.Id, StructureDropMode.Into));

        // A moves into B's scope (end) and is demoted to Heading 2.
        Assert.True(HeadingStylePresets.IsAltHeading(h1a.Style));
        Assert.Equal(new ReportElement[] { h1b, bTable, h1a }, body.Children);
    }

    [Fact]
    public void DragHeadingIntoOwnSubtree_IsRejectedWithoutMutation()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var sub = AltHeading("Sub");
        var subTable = Table("Sub-tab");
        foreach (var e in new ReportElement[] { h1, sub, subTable })
            body.Children.Add(e);
        var before = body.Children.ToList();
        var service = new ReportStructureService();

        // Dropping H1 into its own child Sub is a self-subtree cycle.
        var result = service.Move(report, h1.Id, sub.Id, StructureDropMode.Into);

        Assert.True(result.IsFailure);
        Assert.Equal(before, body.Children);
    }

    [Fact]
    public void CrossSectionMove_IsRejectedWithoutMutation()
    {
        var report = new Report();
        var page = new Page();
        var body1 = new Section { Kind = SectionKind.Body };
        var body2 = new Section { Kind = SectionKind.Body };
        page.Sections.Add(body1);
        page.Sections.Add(body2);
        report.Pages.Add(page);

        var t1 = Table("T1");
        var t2 = Table("T2");
        body1.Root.Children.Add(t1);
        body2.Root.Children.Add(t2);
        var service = new ReportStructureService();

        var result = service.Move(report, t1.Id, t2.Id, StructureDropMode.Before);

        Assert.True(result.IsFailure);
        Assert.Single(body1.Root.Children);
        Assert.Single(body2.Root.Children);
    }

    [Fact]
    public void SelfDrop_IsRejected()
    {
        var (report, body) = NewReport();
        var t = Table("T");
        body.Children.Add(t);
        var service = new ReportStructureService();

        Assert.True(service.Move(report, t.Id, t.Id, StructureDropMode.Before).IsFailure);
    }

    // ---------------------------------------------------------------
    // Selection identity + report-content order
    // ---------------------------------------------------------------

    [Fact]
    public void StructureMutation_PreservesSelectedElementIdentity()
    {
        var (report, body) = NewReport();
        var t1 = Table("T1");
        var t2 = Table("T2");
        body.Children.Add(t1);
        body.Children.Add(t2);
        var service = new ReportStructureService();
        var movedId = t1.Id;

        AssertOk(service.MoveDown(report, t1.Id));

        // The same element instance/Id survives the move (caller keeps it selected).
        var stillThere = body.Children.FirstOrDefault(e => e.Id == movedId);
        Assert.NotNull(stillThere);
        Assert.Same(t1, stillThere);
    }

    [Fact]
    public async Task ReorderedStructure_IsReflectedInReportContentAndWordOrder()
    {
        var (report, body) = NewReport();
        var project = new Project();
        project.Reports.Add(report);
        var a = Heading("Alpha");
        var b = Heading("Beta");
        body.Children.Add(a);
        body.Children.Add(b);
        var service = new ReportStructureService();
        var builder = new ReportContentBuilder(new FakeRegistry());

        var before = await HeadingOrderAsync(builder, project, report);
        Assert.Equal(new[] { "Alpha", "Beta" }, before);

        AssertOk(service.MoveDown(report, a.Id));

        var after = await HeadingOrderAsync(builder, project, report);
        Assert.Equal(new[] { "Beta", "Alpha" }, after);
    }

    // ---------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------

    private static async Task<string[]> HeadingOrderAsync(ReportContentBuilder builder, Project project, Report report)
    {
        var document = await builder.BuildAsync(project, report);
        return document.BodyNodes
            .Where(n => n.Kind is ReportContentKind.Heading or ReportContentKind.AltHeading)
            .OfType<TextContentNode>()
            .Select(n => n.Text)
            .ToArray();
    }

    private sealed class FakeRegistry : KKL.WordStudio.Application.Abstractions.IDataProviderRegistry
    {
        public void Register(KKL.WordStudio.Application.Abstractions.IDataProvider provider) { }
        public KKL.WordStudio.Application.Abstractions.IDataProvider Resolve(string providerKey) =>
            throw new InvalidOperationException("No data provider needed for heading-order test.");
    }

    private static (Report report, Container body) NewReport()
    {
        var report = new Report();
        var page = new Page();
        var body = new Section { Kind = SectionKind.Body };
        page.Sections.Add(body);
        report.Pages.Add(page);
        return (report, body.Root);
    }

    private static TextElement Heading(string text) => new()
    {
        Name = "Heading",
        Style = HeadingStylePresets.CreateHeadingStyle(),
        Content = Expression.Literal(text)
    };

    private static TextElement AltHeading(string text) => new()
    {
        Name = "Alt Heading",
        Style = HeadingStylePresets.CreateAltHeadingStyle(),
        Content = Expression.Literal(text)
    };

    private static TableElement Table(string name) => new() { Name = name };

    private static void AssertOk(KKL.WordStudio.Shared.Results.Result result) =>
        Assert.True(result.IsSuccess, result.Error);
}
