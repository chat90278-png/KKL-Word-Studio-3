namespace KKL.WordStudio.Engine.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Engine.Layout;
using Xunit;

public sealed class Sprint17CaptionSequencePreviewPipelineTests
{
    [Fact]
    public async Task CaptionedTables_ReceiveDocumentOrderSequenceNumbersAndTransportSequenceMetadata()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var sequence = profile.TableCaptionSequence!;
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes =
            [
                CreateTable(firstId, "Birinci açıklama", profile, sequence),
                CreateTable(secondId, "İkinci açıklama", profile, sequence)
            ],
            FooterNodes = [],
            TableOfContents = [],
            PageLayout = new PageLayout
            {
                WidthMillimeters = profile.Page.WidthMillimeters,
                HeightMillimeters = profile.Page.HeightMillimeters,
                MarginTopMillimeters = profile.Page.MarginTopMillimeters,
                MarginBottomMillimeters = profile.Page.MarginBottomMillimeters,
                MarginLeftMillimeters = profile.Page.MarginLeftMillimeters,
                MarginRightMillimeters = profile.Page.MarginRightMillimeters,
                ShowPageNumbers = false
            }
        };

        var layout = await new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });

        var payloads = layout.Pages
            .SelectMany(page => page.Blocks)
            .Where(block => block.Kind == PageBlockKind.Table && block.FragmentIndex == 0)
            .Select(block => Assert.IsType<TablePageBlockPayload>(block.Payload))
            .ToList();

        Assert.Equal(2, payloads.Count);
        Assert.Same(sequence, payloads[0].CaptionSequence);
        Assert.Same(sequence, payloads[1].CaptionSequence);
        Assert.Equal(1, payloads[0].CaptionSequenceNumber);
        Assert.Equal(2, payloads[1].CaptionSequenceNumber);
        Assert.Equal("Birinci açıklama", payloads[0].Caption);
        Assert.Equal("İkinci açıklama", payloads[1].Caption);
    }

    [Fact]
    public async Task BlankCaption_DoesNotConsumeASequenceNumber()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var sequence = profile.TableCaptionSequence!;
        var document = new ReportContentDocument
        {
            HeaderNodes = [],
            BodyNodes =
            [
                CreateTable(Guid.NewGuid(), null, profile, sequence),
                CreateTable(Guid.NewGuid(), "İlk görünen başlık", profile, sequence)
            ],
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

        var layout = await new DeterministicDocumentLayoutEngine().LayoutAsync(new DocumentLayoutRequest
        {
            ReportContent = document,
            FrontMatter = null
        });
        var payloads = layout.Pages
            .SelectMany(page => page.Blocks)
            .Where(block => block.Kind == PageBlockKind.Table && block.FragmentIndex == 0)
            .Select(block => Assert.IsType<TablePageBlockPayload>(block.Payload))
            .ToList();

        Assert.Null(payloads[0].CaptionSequenceNumber);
        Assert.Equal(1, payloads[1].CaptionSequenceNumber);
    }

    private static TableContentNode CreateTable(
        Guid id,
        string? caption,
        DocumentFormatProfile profile,
        TableCaptionSequenceProfile sequence) => new()
    {
        ElementId = id,
        Kind = ReportContentKind.Table,
        Name = "Caption sequence table",
        Caption = caption,
        CaptionFormat = profile.TableCaption,
        CaptionSequence = sequence,
        ColumnHeaders = ["Column"],
        Rows = [["Value"]],
        Format = profile.TableFormats[0].Format,
        DataSourceName = null,
        SourceCount = 0,
        SourceError = null,
        FilterWasIgnored = false
    };
}
