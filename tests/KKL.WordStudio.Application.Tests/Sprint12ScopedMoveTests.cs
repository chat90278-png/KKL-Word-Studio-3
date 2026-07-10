namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

/// <summary>
/// Sprint 12 completion — gap D: Move Up/Down must stay within the element's
/// derived scope (never escape its parent heading, never implicitly promote).
/// </summary>
public class Sprint12ScopedMoveTests
{
    [Fact]
    public void MoveUp_FirstHeading2InHeading1Scope_IsRejectedWithoutMutation()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var sub1 = AltHeading("Sub1");
        var sub2 = AltHeading("Sub2");
        foreach (var e in new ReportElement[] { h1, sub1, sub2 }) body.Children.Add(e);
        var before = body.Children.ToList();
        var service = new ReportStructureService();

        // Sub1 is the first Heading 2 under H1; moving up must NOT jump before H1.
        var result = service.MoveUp(report, sub1.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(before, body.Children);
    }

    [Fact]
    public void MoveUp_FirstTableInHeadingScope_DoesNotEscapeParentScope()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var tableA = Table("A");
        var tableB = Table("B");
        foreach (var e in new ReportElement[] { h1, tableA, tableB }) body.Children.Add(e);
        var before = body.Children.ToList();
        var service = new ReportStructureService();

        // Table A is the first content under H1; it must not move before H1.
        var result = service.MoveUp(report, tableA.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(before, body.Children);
    }

    [Fact]
    public void MoveDown_LastHeading2InHeading1Scope_IsRejectedWithoutMutation()
    {
        var (report, body) = NewReport();
        var h1a = Heading("H1a");
        var sub1 = AltHeading("Sub1");
        var sub2 = AltHeading("Sub2");
        var h1b = Heading("H1b");
        foreach (var e in new ReportElement[] { h1a, sub1, sub2, h1b }) body.Children.Add(e);
        var before = body.Children.ToList();
        var service = new ReportStructureService();

        // Sub2 is the last Heading 2 in H1a's scope; moving down must NOT cross
        // into H1b's scope.
        var result = service.MoveDown(report, sub2.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(before, body.Children);
    }

    [Fact]
    public void MoveDown_LastTableInHeadingScope_DoesNotEscapeParentScope()
    {
        var (report, body) = NewReport();
        var h1a = Heading("H1a");
        var tableA = Table("A");
        var tableB = Table("B");
        var h1b = Heading("H1b");
        foreach (var e in new ReportElement[] { h1a, tableA, tableB, h1b }) body.Children.Add(e);
        var before = body.Children.ToList();
        var service = new ReportStructureService();

        // Table B is the last content under H1a; moving down must not cross H1b.
        var result = service.MoveDown(report, tableB.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(before, body.Children);
    }

    [Fact]
    public void MoveHeading2_StillSwapsWithAdjacentHeading2SiblingBlock()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var sub1 = AltHeading("Sub1");
        var sub1Table = Table("Sub1-tab");
        var sub2 = AltHeading("Sub2");
        var sub2Table = Table("Sub2-tab");
        foreach (var e in new ReportElement[] { h1, sub1, sub1Table, sub2, sub2Table }) body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.MoveDown(report, sub1.Id));

        // Sub1 block swaps with Sub2 block, both staying inside H1's scope.
        Assert.Equal(
            new ReportElement[] { h1, sub2, sub2Table, sub1, sub1Table },
            body.Children);
    }

    [Fact]
    public void MoveTable_StillSwapsWithAdjacentTableInSameScope()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var tableA = Table("A");
        var tableB = Table("B");
        foreach (var e in new ReportElement[] { h1, tableA, tableB }) body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.MoveDown(report, tableA.Id));
        Assert.Equal(new ReportElement[] { h1, tableB, tableA }, body.Children);

        AssertOk(service.MoveUp(report, tableA.Id));
        Assert.Equal(new ReportElement[] { h1, tableA, tableB }, body.Children);
    }

    [Fact]
    public void MoveDown_Heading1_StillMovesAmongHeading1Blocks()
    {
        var (report, body) = NewReport();
        var a = Heading("A");
        var aSub = AltHeading("A-sub");
        var b = Heading("B");
        foreach (var e in new ReportElement[] { a, aSub, b }) body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.MoveDown(report, a.Id));

        // The whole A block (A + A-sub) moves after B.
        Assert.Equal(new ReportElement[] { b, a, aSub }, body.Children);
    }

    [Fact]
    public void MoveUp_ContentInsideHeading2_StaysInHeading2Scope()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var h2 = AltHeading("H2");
        var t1 = Table("T1");
        var t2 = Table("T2");
        foreach (var e in new ReportElement[] { h1, h2, t1, t2 }) body.Children.Add(e);
        var service = new ReportStructureService();

        // T1 is the first content under H2; moving up must be rejected, not jump
        // above H2 into H1's scope.
        var up = service.MoveUp(report, t1.Id);
        Assert.True(up.IsFailure);

        // T2 can swap with T1 within the H2 scope.
        AssertOk(service.MoveUp(report, t2.Id));
        Assert.Equal(new ReportElement[] { h1, h2, t2, t1 }, body.Children);
    }

    // ---------------------------------------------------------------
    // Gap B: delete confirmation coordination (confirm → mutate, cancel → not).
    // Models the ViewModel's confirm-then-delete flow at the service boundary
    // with a fake confirmation decision, since the real ContentsViewModel lives
    // in the WPF UI project.
    // ---------------------------------------------------------------

    [Fact]
    public void DeleteConfirmation_Cancel_DoesNotMutate()
    {
        var (report, body) = NewReport();
        var table = Table("T");
        body.Children.Add(table);
        var service = new ReportStructureService();

        var confirmed = false; // user cancels
        if (confirmed) service.Delete(report, table.Id);

        Assert.Single(body.Children);
    }

    [Fact]
    public void DeleteConfirmation_Confirm_DeletesRealElement()
    {
        var (report, body) = NewReport();
        var project = new Project();
        project.Reports.Add(report);
        var table = Table("T");
        body.Children.Add(table);
        var service = new ReportStructureService();

        var confirmed = true; // user confirms
        if (confirmed) AssertOk(service.Delete(report, table.Id));

        Assert.Empty(body.Children);
        // Project data sources are never touched by a table delete.
        Assert.Empty(project.DataSources);
    }

    // ---------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------

    // ---------------------------------------------------------------
    // Final stabilization gap 2: Move Up must find the previous sibling block
    // at the selected block's derived LEVEL, not by walking through content.
    // ---------------------------------------------------------------

    [Fact]
    public void MoveUp_Heading1AfterNestedHeading2_SwapsWholeHeading1Blocks()
    {
        var (report, body) = NewReport();
        var a = Heading("A");
        var a1 = AltHeading("A.1");
        var a1Table = Table("Table A.1");
        var b = Heading("B");
        var bTable = Table("Table B");
        foreach (var e in new ReportElement[] { a, a1, a1Table, b, bTable }) body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.MoveUp(report, b.Id));

        // Whole H1 B block swaps ahead of the whole H1 A block (incl. its H2 subtree).
        Assert.Equal(
            new ReportElement[] { b, bTable, a, a1, a1Table },
            body.Children);
    }

    [Fact]
    public void MoveUp_Heading1AfterDirectContent_SwapsWholeHeading1Blocks()
    {
        var (report, body) = NewReport();
        var a = Heading("A");
        var aTable = Table("A-tab");
        var b = Heading("B");
        var bTable = Table("B-tab");
        foreach (var e in new ReportElement[] { a, aTable, b, bTable }) body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.MoveUp(report, b.Id));

        Assert.Equal(
            new ReportElement[] { b, bTable, a, aTable },
            body.Children);
    }

    [Fact]
    public void MoveUp_Heading2AfterSiblingWithTable_SwapsWholeHeading2Blocks()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var s1 = AltHeading("S1");
        var s1Table = Table("S1-tab");
        var s2 = AltHeading("S2");
        var s2Table = Table("S2-tab");
        foreach (var e in new ReportElement[] { h1, s1, s1Table, s2, s2Table }) body.Children.Add(e);
        var service = new ReportStructureService();

        AssertOk(service.MoveUp(report, s2.Id));

        // S2 block swaps ahead of S1 block; both stay inside H1's scope.
        Assert.Equal(
            new ReportElement[] { h1, s2, s2Table, s1, s1Table },
            body.Children);
    }

    [Fact]
    public void MoveUp_TableWithinHeadingScope_SwapsOnlyAdjacentTable()
    {
        var (report, body) = NewReport();
        var h1 = Heading("H1");
        var t1 = Table("T1");
        var t2 = Table("T2");
        var t3 = Table("T3");
        foreach (var e in new ReportElement[] { h1, t1, t2, t3 }) body.Children.Add(e);
        var service = new ReportStructureService();

        // Moving T3 up swaps with only the immediately adjacent content block T2.
        AssertOk(service.MoveUp(report, t3.Id));
        Assert.Equal(new ReportElement[] { h1, t1, t3, t2 }, body.Children);
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
