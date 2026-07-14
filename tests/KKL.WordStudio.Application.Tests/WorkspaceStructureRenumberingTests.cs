namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Workspace;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class WorkspaceStructureRenumberingTests
{
    [Fact]
    public void NotifyReportContentChanged_RestoresHeadingNumberAfterExternalEdit()
    {
        var workspace = new Workspace();
        var report = new Report { Name = "Report" };
        var page = new Page();
        var body = new Section { Name = "Body", Kind = SectionKind.Body, AutoHeight = true };
        var heading = new TextElement
        {
            Name = "Heading",
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal("Heading")
        };
        body.Root.Children.Add(heading);
        page.Sections.Add(body);
        report.Pages.Add(page);
        workspace.SetActiveReport(report);

        heading.Content = Expression.Literal("Edited outside Contents");
        workspace.NotifyReportContentChanged();

        Assert.Equal("1.1 Edited outside Contents", heading.Content.Text);
    }
}
