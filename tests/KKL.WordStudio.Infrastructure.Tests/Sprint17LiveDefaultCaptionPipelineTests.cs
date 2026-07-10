namespace KKL.WordStudio.Infrastructure.Tests;

using KKL.WordStudio.Application.Abstractions;
using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Tables;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Infrastructure.ReferenceFormatting;
using Xunit;

public sealed class Sprint17LiveDefaultCaptionPipelineTests
{
    [Fact]
    public async Task NoReference_RealProviderAndBuilder_TransportBuiltInCaptionFormatAndSequence()
    {
        var project = new Project();
        var report = new Report();
        var page = new Page();
        var section = new Section { Kind = SectionKind.Body };
        var table = new TableElement
        {
            Name = "Captioned table",
            Caption = "deneme başlık"
        };
        table.Columns.Add(new TableColumn { Header = "No" });
        section.Root.Children.Add(table);
        page.Sections.Add(section);
        report.Pages.Add(page);

        var builder = new ReportContentBuilder(
            new NoOpRegistry(),
            new PassthroughTableContentRowComposer(),
            new OpenXmlReferenceDocumentFormatProvider(),
            new ReferenceReportContentFormatResolver());

        var document = await builder.BuildAsync(project, report);

        var tableNode = Assert.IsType<TableContentNode>(Assert.Single(document.BodyNodes));
        Assert.Equal("deneme başlık", tableNode.Caption);
        var captionFormat = Assert.IsType<ResolvedTextFormat>(tableNode.CaptionFormat);
        Assert.Equal("Arial", captionFormat.FontFamilyName);
        Assert.Equal(8d, captionFormat.FontSizePoints, 3);
        Assert.True(captionFormat.Bold);
        Assert.Equal("#FF000000", captionFormat.ForegroundColor);
        Assert.Equal(ParagraphAlignment.Center, captionFormat.Alignment);

        var sequence = Assert.IsType<TableCaptionSequenceProfile>(tableNode.CaptionSequence);
        Assert.Equal("Tablo", sequence.DisplayLabel);
        Assert.Equal("Tablo", sequence.SequenceIdentifier);
        Assert.Equal(": ", sequence.Separator);
    }

    private sealed class NoOpRegistry : IDataProviderRegistry
    {
        public void Register(IDataProvider provider) { }

        public IDataProvider Resolve(string providerKey) =>
            throw new InvalidOperationException("Static caption regression must not resolve a data provider.");
    }
}
