namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class ReportHeadingNumberingServiceTests
{
    [Fact]
    public void Renumber_UsesRootHeadingAndAltHeadingHierarchy()
    {
        var (report, body) = CreateReport();
        body.Root.Children.Add(Heading("Document Root", "System Test Procedure Configuration List", alt: false));
        body.Root.Children.Add(Heading("Heading", "Assembly", alt: false));
        body.Root.Children.Add(Heading("Alt Heading", "Parts", alt: true));
        body.Root.Children.Add(Heading("Heading", "Tools", alt: false));
        body.Root.Children.Add(Heading("Alt Heading", "Special Tools", alt: true));

        ReportHeadingNumberingService.Renumber(report);

        Assert.Equal(
            [
                "1. System Test Procedure Configuration List",
                "1.1 Assembly",
                "1.1.1 Parts",
                "1.2 Tools",
                "1.2.1 Special Tools"
            ],
            body.Root.Children.OfType<TextElement>().Select(text => text.Content.Text));
    }

    [Fact]
    public void Renumber_IsIdempotentAfterRepeatedPreviewRefreshes()
    {
        var (report, body) = CreateReport();
        body.Root.Children.Add(Heading("Document Root", "1. System Test Procedure Configuration List", alt: false));
        body.Root.Children.Add(Heading("Heading", "1.1 Assembly", alt: false));
        body.Root.Children.Add(Heading("Alt Heading", "1.1.1 Parts", alt: true));

        ReportHeadingNumberingService.Renumber(report);
        ReportHeadingNumberingService.Renumber(report);

        Assert.Equal("1. System Test Procedure Configuration List", ((TextElement)body.Root.Children[0]).Content.Text);
        Assert.Equal("1.1 Assembly", ((TextElement)body.Root.Children[1]).Content.Text);
        Assert.Equal("1.1.1 Parts", ((TextElement)body.Root.Children[2]).Content.Text);
    }

    [Fact]
    public void Renumber_DoesNotChangeNormalBodyText()
    {
        var (report, body) = CreateReport();
        var bodyText = new TextElement
        {
            Name = "Body Text",
            Content = Expression.Literal("2026 release notes")
        };
        body.Root.Children.Add(bodyText);

        ReportHeadingNumberingService.Renumber(report);

        Assert.Equal("2026 release notes", bodyText.Content.Text);
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

    private static TextElement Heading(string name, string text, bool alt) => new()
    {
        Name = name,
        Style = alt ? HeadingStylePresets.CreateAltHeadingStyle() : HeadingStylePresets.CreateHeadingStyle(),
        Content = Expression.Literal(text)
    };
}
