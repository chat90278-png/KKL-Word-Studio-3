namespace KKL.WordStudio.Infrastructure.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Styling;
using KKL.WordStudio.Infrastructure.ReferenceFormatting;
using Xunit;

public sealed class Sprint17BuiltInDefaultFormatTests
{
    [Fact]
    public void DefaultProfile_UsesSupportedBuiltInGeometry()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();

        Assert.InRange(profile.Page.WidthMillimeters, 209.99d, 210.02d);
        Assert.InRange(profile.Page.HeightMillimeters, 296.99d, 297.02d);
        Assert.InRange(profile.Page.MarginLeftMillimeters, 24.98d, 25.01d);
        Assert.InRange(profile.Page.MarginRightMillimeters, 24.98d, 25.01d);
        Assert.InRange(profile.Page.HeaderDistanceMillimeters, 12.47d, 12.50d);
        Assert.InRange(profile.Page.FooterDistanceMillimeters, 12.47d, 12.50d);

        Assert.Equal("Arial", profile.BodyText.FontFamilyName);
        Assert.Equal(10d, profile.BodyText.FontSizePoints, 3);
        Assert.Equal(ParagraphAlignment.Left, profile.BodyText.Alignment);
        Assert.Equal(1d, profile.BodyText.LineSpacingMultiple, 3);

        Assert.Equal(12d, profile.PrimaryHeading.FontSizePoints, 3);
        Assert.True(profile.PrimaryHeading.Bold);
        Assert.True(profile.PrimaryHeading.Italic);
        Assert.True(profile.PrimaryHeading.KeepWithNext);
        Assert.Equal(4d, profile.PrimaryHeading.SpaceBeforePoints, 3);
        Assert.Equal(2d, profile.PrimaryHeading.SpaceAfterPoints, 3);

        Assert.Equal(12d, profile.SecondaryHeading.FontSizePoints, 3);
        Assert.True(profile.SecondaryHeading.Bold);
        Assert.False(profile.SecondaryHeading.Italic);
        Assert.True(profile.SecondaryHeading.KeepWithNext);
        Assert.InRange(profile.SecondaryHeading.LeftIndentMillimeters, 21.7d, 21.8d);

        Assert.Equal("Arial", profile.TableCaption.FontFamilyName);
        Assert.Equal(8d, profile.TableCaption.FontSizePoints, 3);
        Assert.True(profile.TableCaption.Bold);
        Assert.Equal("#FF000000", profile.TableCaption.ForegroundColor);
        Assert.Equal(ParagraphAlignment.Center, profile.TableCaption.Alignment);
        Assert.True(profile.TableCaption.KeepWithNext);
        Assert.Equal(2d, profile.TableCaption.LineSpacingMultiple, 3);
        Assert.Equal("Tablo", profile.TableCaptionSequence!.DisplayLabel);
        Assert.Equal("Tablo", profile.TableCaptionSequence.SequenceIdentifier);
        Assert.Equal(": ", profile.TableCaptionSequence.Separator);

        Assert.Equal(2, profile.TableFormats.Count);
        Assert.Equal(
            new[] { 5.13d, 30.04d, 15.49d, 18.33d, 21.08d, 9.93d },
            profile.TableFormats[0].Format.Columns.Select(column => column.WidthWeight));
        Assert.Equal(
            new[] { 5.21d, 28.33d, 17.54d, 17.54d, 20.02d, 11.34d },
            profile.TableFormats[1].Format.Columns.Select(column => column.WidthWeight));
        Assert.All(profile.TableFormats, table => Assert.Equal(10.195d, table.Format.PreferredRowHeightMillimeters, 3));
        Assert.All(profile.TableFormats, table => Assert.Equal(1.235d, table.Format.CellMarginLeftMillimeters, 3));
    }

    [Fact]
    public void ReferenceAwareResolver_UsesBuiltInDefaultWhenProfileIsNull()
    {
        var resolver = new ReferenceReportContentFormatResolver();
        var authoredPage = new PageLayout
        {
            WidthMillimeters = 180d,
            HeightMillimeters = 240d,
            MarginTopMillimeters = 5d,
            MarginBottomMillimeters = 6d,
            MarginLeftMillimeters = 7d,
            MarginRightMillimeters = 8d,
            ShowPageNumbers = false
        };

        var page = resolver.ResolvePageLayout(null, authoredPage);
        var text = resolver.ResolveText(null, ReportContentKind.Paragraph, new Style());

        Assert.InRange(page.WidthMillimeters, 209.99d, 210.02d);
        Assert.InRange(page.HeightMillimeters, 296.99d, 297.02d);
        Assert.InRange(page.MarginLeftMillimeters, 24.98d, 25.01d);
        Assert.InRange(page.MarginRightMillimeters, 24.98d, 25.01d);
        Assert.False(page.ShowPageNumbers);
        Assert.Equal("Arial", text.FontFamilyName);
        Assert.Equal(10d, text.FontSizePoints, 3);
    }

    [Fact]
    public async Task NoProjectReference_ReturnsCompleteBuiltInProfileIncludingCaptionMetadata()
    {
        var result = await new OpenXmlReferenceDocumentFormatProvider().ReadAsync(new Project());

        Assert.False(result.IsMissing);
        Assert.Null(result.StatusMessage);
        var profile = Assert.IsType<DocumentFormatProfile>(result.Profile);
        Assert.Equal("Arial", profile.TableCaption.FontFamilyName);
        Assert.Equal(8d, profile.TableCaption.FontSizePoints, 3);
        Assert.Equal(ParagraphAlignment.Center, profile.TableCaption.Alignment);
        Assert.Equal("#FF000000", profile.TableCaption.ForegroundColor);
        Assert.Equal("Tablo", profile.TableCaptionSequence!.DisplayLabel);
        Assert.Equal(": ", profile.TableCaptionSequence.Separator);
    }

    [Fact]
    public async Task MissingProjectReference_PreservesMissingStateAndReturnsDefaultFallbackProfile()
    {
        var project = new Project
        {
            ReferenceFormat = new ReferenceFormatDocument
            {
                FileName = "Kayip-Bicim.docx",
                OriginalSourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Kayip-Bicim.docx")
            }
        };

        var result = await new OpenXmlReferenceDocumentFormatProvider().ReadAsync(project);

        Assert.True(result.IsMissing);
        var profile = Assert.IsType<DocumentFormatProfile>(result.Profile);
        Assert.Equal("Tablo", profile.TableCaptionSequence!.DisplayLabel);
        Assert.Equal(": ", profile.TableCaptionSequence.Separator);
        Assert.Contains("Biçim şablonu bulunamadı", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Varsayılan KKL belge biçimi kullanılacak", result.StatusMessage, StringComparison.Ordinal);
    }
}
