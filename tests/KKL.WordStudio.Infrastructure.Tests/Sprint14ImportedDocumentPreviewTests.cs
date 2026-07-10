namespace KKL.WordStudio.Infrastructure.Tests;

using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KKL.WordStudio.Application.ImportedDocuments;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Infrastructure.Word;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

public sealed class Sprint14ImportedDocumentPreviewTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task ImportedPreview_NoFrontMatter_ReturnsNullDocument()
    {
        var result = await new OpenXmlImportedDocumentPreviewProvider().ReadAsync(new Project { Name = "No Front Matter" });

        Assert.Null(result.Document);
        Assert.False(result.IsMissing);
        Assert.Null(result.StatusMessage);
    }

    [Fact]
    public async Task ImportedPreview_MissingAsset_ReturnsMissingState()
    {
        var project = new Project { Name = "Missing" };
        project.FrontMatter = new FrontMatterDocument
        {
            FileName = "KayipKapak.docx",
            OriginalSourcePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".docx")
        };

        var result = await new OpenXmlImportedDocumentPreviewProvider().ReadAsync(project);

        Assert.Null(result.Document);
        Assert.True(result.IsMissing);
        Assert.Contains("KayipKapak.docx", result.StatusMessage);
    }

    [Fact]
    public async Task ImportedPreview_DoesNotModifySourceDocx()
    {
        var path = CreateDocx((_, body) => body.Append(new Paragraph(new Run(new Text("Salt okunur kaynak")))));
        try
        {
            var before = SHA256.HashData(await File.ReadAllBytesAsync(path));

            _ = await ReadAsync(path);

            var after = SHA256.HashData(await File.ReadAllBytesAsync(path));
            Assert.Equal(before, after);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_ExtractsParagraphTextAndRunFormatting()
    {
        var path = CreateDocx((_, body) => body.Append(
            new Paragraph(
                new Run(
                    new RunProperties(
                        new Bold(),
                        new Italic(),
                        new Underline { Val = UnderlineValues.Single },
                        new RunFonts { Ascii = "Aptos", HighAnsi = "Aptos" },
                        new FontSize { Val = "28" }),
                    new Text("Biçimli metin")))));
        try
        {
            var result = await ReadAsync(path);
            var paragraph = Assert.IsType<ImportedParagraphBlock>(Assert.Single(Assert.Single(result.Document!.Sections).Blocks));
            var run = Assert.Single(paragraph.Runs);

            Assert.Equal("Biçimli metin", run.Text);
            Assert.True(run.Bold);
            Assert.True(run.Italic);
            Assert.True(run.Underline);
            Assert.Equal(14, run.FontSizePoints);
            Assert.Equal("Aptos", run.FontFamilyName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_ResolvesParagraphStyleFormattingAndKeepNext()
    {
        var path = CreateDocx((mainPart, body) =>
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = new Styles(
                new Style
                {
                    Type = StyleValues.Paragraph,
                    StyleId = "PreviewHeading",
                    StyleName = new StyleName { Val = "Preview Heading" },
                    StyleParagraphProperties = new StyleParagraphProperties(
                        new Justification { Val = JustificationValues.Center },
                        new KeepNext()),
                    StyleRunProperties = new StyleRunProperties(
                        new Bold(),
                        new FontSize { Val = "32" })
                });
            stylesPart.Styles.Save(stylesPart);

            body.Append(new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = "PreviewHeading" }),
                new Run(new Text("Stilli başlık"))));
        });
        try
        {
            var result = await ReadAsync(path);
            var paragraph = Assert.IsType<ImportedParagraphBlock>(Assert.Single(Assert.Single(result.Document!.Sections).Blocks));
            var run = Assert.Single(paragraph.Runs);

            Assert.Equal(ParagraphAlignment.Center, paragraph.Alignment);
            Assert.True(paragraph.KeepWithNext);
            Assert.True(run.Bold);
            Assert.Equal(16, run.FontSizePoints);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_ExtractsParagraphAlignment()
    {
        var path = CreateDocx((_, body) => body.Append(
            new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                new Run(new Text("Sağda")))));
        try
        {
            var result = await ReadAsync(path);
            var paragraph = Assert.IsType<ImportedParagraphBlock>(Assert.Single(Assert.Single(result.Document!.Sections).Blocks));
            Assert.Equal(ParagraphAlignment.Right, paragraph.Alignment);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_ExtractsTableRows()
    {
        var path = CreateDocx((_, body) => body.Append(
            new Table(
                new TableRow(
                    new TableRowProperties(new TableHeader()),
                    Cell("Başlık 1"),
                    Cell("Başlık 2")),
                new TableRow(Cell("A"), Cell("")),
                new TableRow(Cell("B1\nB2"), Cell("C")))));
        try
        {
            var result = await ReadAsync(path);
            var table = Assert.IsType<ImportedTableBlock>(Assert.Single(Assert.Single(result.Document!.Sections).Blocks));

            Assert.True(table.RepeatFirstRow);
            Assert.Equal(3, table.Rows.Count);
            Assert.Equal(new[] { "Başlık 1", "Başlık 2" }, table.Rows[0]);
            Assert.Equal(string.Empty, table.Rows[1][1]);
            Assert.Equal("B1\nB2", table.Rows[2][0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_ExtractsInlineImageBytesAndDimensions()
    {
        var path = CreateDocx((mainPart, body) =>
        {
            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var imageStream = new MemoryStream(OnePixelPng))
                imagePart.FeedData(imageStream);
            var relationshipId = mainPart.GetIdOfPart(imagePart);

            body.Append(new Paragraph(new Run(BuildInlineImage(relationshipId, 360_000L, 720_000L))));
        });
        try
        {
            var result = await ReadAsync(path);
            var image = Assert.IsType<ImportedImageBlock>(Assert.Single(Assert.Single(result.Document!.Sections).Blocks));

            Assert.Equal(OnePixelPng, image.ImageBytes);
            Assert.Equal("image/png", image.ContentType);
            Assert.Equal(10, image.WidthMillimeters!.Value, 6);
            Assert.Equal(20, image.HeightMillimeters!.Value, 6);
            Assert.Equal("Preview fixture", image.Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_ExtractsExplicitPageBreak()
    {
        var path = CreateDocx((_, body) => body.Append(
            new Paragraph(
                new Run(new Text("Önce"), new Break { Type = BreakValues.Page }, new Text("Sonra")))));
        try
        {
            var result = await ReadAsync(path);
            var blocks = Assert.Single(result.Document!.Sections).Blocks;

            Assert.Collection(
                blocks,
                block => Assert.Equal("Önce", Assert.Single(Assert.IsType<ImportedParagraphBlock>(block).Runs).Text),
                block => Assert.IsType<ImportedExplicitPageBreakBlock>(block),
                block => Assert.Equal("Sonra", Assert.Single(Assert.IsType<ImportedParagraphBlock>(block).Runs).Text));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_UsesSectionPageGeometry()
    {
        var path = CreateDocx((_, body) =>
        {
            body.Append(new Paragraph(new Run(new Text("Yatay bölüm"))));
            body.Append(new SectionProperties(
                new PageSize { Width = 15_840U, Height = 12_240U, Orient = PageOrientationValues.Landscape },
                new PageMargin { Top = 720, Bottom = 720, Left = 1_440U, Right = 1_440U }));
        });
        try
        {
            var result = await ReadAsync(path);
            var layout = Assert.Single(result.Document!.Sections).PageLayout;

            Assert.True(layout.WidthMillimeters > layout.HeightMillimeters);
            Assert.Equal(279.4, layout.WidthMillimeters, 1);
            Assert.Equal(215.9, layout.HeightMillimeters, 1);
            Assert.Equal(12.7, layout.MarginTopMillimeters, 1);
            Assert.Equal(25.4, layout.MarginLeftMillimeters, 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_UnsupportedShape_IsWarnedOrPlaceholder()
    {
        var path = CreateDocx((_, body) => body.Append(new Paragraph(new Run(new Picture()))));
        try
        {
            var result = await ReadAsync(path);
            var document = result.Document!;

            Assert.NotEmpty(document.Warnings);
            Assert.Contains(Assert.Single(document.Sections).Blocks, block => block is ImportedUnsupportedBlock);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportedPreview_PreservesDocumentOrderAcrossParagraphTableImage()
    {
        var path = CreateDocx((mainPart, body) =>
        {
            body.Append(new Paragraph(new Run(new Text("Paragraf"))));
            body.Append(new Table(new TableRow(Cell("Tablo"))));

            var imagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var imageStream = new MemoryStream(OnePixelPng))
                imagePart.FeedData(imageStream);
            body.Append(new Paragraph(new Run(BuildInlineImage(mainPart.GetIdOfPart(imagePart), 36_000L, 36_000L))));
        });
        try
        {
            var result = await ReadAsync(path);
            var blocks = Assert.Single(result.Document!.Sections).Blocks;

            Assert.Collection(
                blocks,
                block => Assert.IsType<ImportedParagraphBlock>(block),
                block => Assert.IsType<ImportedTableBlock>(block),
                block => Assert.IsType<ImportedImageBlock>(block));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<ImportedDocumentPreviewResult> ReadAsync(string path)
    {
        var project = new Project { Name = "Preview Fixture" };
        project.FrontMatter = new FrontMatterDocument
        {
            FileName = Path.GetFileName(path),
            OriginalSourcePath = path,
            ResolvedFilePath = path
        };
        return await new OpenXmlImportedDocumentPreviewProvider().ReadAsync(project);
    }

    private static TableCell Cell(string text)
    {
        var paragraphs = text.Split('\n')
            .Select(value => new Paragraph(new Run(new Text(value))))
            .Cast<OpenXmlElement>()
            .ToArray();
        return new TableCell(paragraphs);
    }

    private static Drawing BuildInlineImage(string relationshipId, long widthEmus, long heightEmus) =>
        new(
            new DW.Inline(
                new DW.Extent { Cx = widthEmus, Cy = heightEmus },
                new DW.DocProperties { Id = 1U, Name = "Preview fixture" },
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "fixture.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthEmus, Cy = heightEmus }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });

    private static string CreateDocx(Action<MainDocumentPart, Body> populate)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".docx");
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document, autoSave: false);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());
        populate(mainPart, body);
        mainPart.Document.Save();
        return path;
    }
}
