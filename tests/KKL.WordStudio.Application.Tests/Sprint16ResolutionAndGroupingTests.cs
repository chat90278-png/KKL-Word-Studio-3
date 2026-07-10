namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Content;
using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Application.Layout;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Styling;
using Xunit;

public sealed class Sprint16ResolutionAndGroupingTests
{
    private readonly ReferenceReportContentFormatResolver resolver = new();

    [Fact]
    public void Resolver_UsesReferencePrimaryHeading()
    {
        var profile = CreateProfile();

        var actual = resolver.ResolveText(profile, ReportContentKind.Heading, HeadingStylePresets.CreateHeadingStyle());

        Assert.Equal("Arial", actual.FontFamilyName);
        Assert.Equal(12d, actual.FontSizePoints);
        Assert.True(actual.Bold);
        Assert.True(actual.Italic);
        Assert.True(actual.KeepWithNext);
        Assert.Equal(4d, actual.SpaceBeforePoints);
        Assert.Equal(2d, actual.SpaceAfterPoints);
    }

    [Fact]
    public void Resolver_UsesReferenceSecondaryHeading()
    {
        var profile = CreateProfile();

        var actual = resolver.ResolveText(profile, ReportContentKind.AltHeading, HeadingStylePresets.CreateAltHeadingStyle());

        Assert.Equal("Arial", actual.FontFamilyName);
        Assert.Equal(12d, actual.FontSizePoints);
        Assert.True(actual.Bold);
        Assert.False(actual.Italic);
        Assert.Equal(6.5d, actual.LeftIndentMillimeters);
        Assert.True(actual.KeepWithNext);
    }

    [Fact]
    public void Resolver_DefaultStyleDoesNotOverrideReference()
    {
        var actual = resolver.ResolveText(CreateProfile(), ReportContentKind.Paragraph, new Style());

        Assert.Equal("Arial", actual.FontFamilyName);
        Assert.Equal(10d, actual.FontSizePoints);
        Assert.Equal(ParagraphAlignment.Justify, actual.Alignment);
        Assert.Equal(1.15d, actual.LineSpacingMultiple);
    }

    [Fact]
    public void Resolver_ExplicitTextStyleOverrideWins()
    {
        var style = new Style
        {
            FontFamily = "Calibri",
            FontSize = 11d,
            Bold = true,
            Italic = true,
            Underline = true,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var actual = resolver.ResolveText(CreateProfile(), ReportContentKind.Paragraph, style);

        Assert.Equal("Calibri", actual.FontFamilyName);
        Assert.Equal(11d, actual.FontSizePoints);
        Assert.True(actual.Bold);
        Assert.True(actual.Italic);
        Assert.True(actual.Underline);
        Assert.Equal(ParagraphAlignment.Center, actual.Alignment);
    }

    [Fact]
    public void Resolver_SelectsExplicitReferenceTableKey()
    {
        var table = CreateTable("A", "B", "C");
        table.ReferenceTableFormatKey = "table-2";

        var actual = resolver.ResolveTable(CreateProfile(), table);

        Assert.Equal(99.32d, actual.WidthPercent);
        Assert.Equal(3, actual.Columns.Count);
        Assert.Equal(4d, actual.Columns[0].WidthWeight);
    }

    [Fact]
    public void Resolver_NullTableKey_SelectsSameColumnCountProfile()
    {
        var table = CreateTable("A", "B", "C");

        var actual = resolver.ResolveTable(CreateProfile(), table);

        Assert.Equal(99.32d, actual.WidthPercent);
        Assert.Equal(3, actual.Columns.Count);
    }

    [Fact]
    public void Resolver_ReferencePageGeometryWins()
    {
        var authored = new PageLayout
        {
            WidthMillimeters = 100,
            HeightMillimeters = 200,
            MarginTopMillimeters = 5,
            MarginBottomMillimeters = 5,
            MarginLeftMillimeters = 5,
            MarginRightMillimeters = 5,
            HeaderDistanceMillimeters = 3,
            FooterDistanceMillimeters = 3,
            ShowPageNumbers = true
        };

        var actual = resolver.ResolvePageLayout(CreateProfile(), authored);

        Assert.Equal(210d, actual.WidthMillimeters);
        Assert.Equal(297d, actual.HeightMillimeters);
        Assert.Equal(25d, actual.MarginLeftMillimeters);
        Assert.Equal(12.49d, actual.HeaderDistanceMillimeters);
        Assert.Equal(12.49d, actual.FooterDistanceMillimeters);
        Assert.True(actual.ShowPageNumbers);
    }

    [Fact]
    public void GroupingDiagnosis_MissingQuantityExplainsAdetColumn()
    {
        var service = CreateGroupingService();
        var table = CreateTable("Parça Numarası", "Seri Numarası", "NSN");

        var diagnosis = service.Diagnose(table);

        Assert.False(diagnosis.IsConfigured);
        Assert.Contains("Adet sütunu bulunamadı", diagnosis.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupingDiagnosis_ConfiguredShowsStableColumns()
    {
        var service = CreateGroupingService();
        var table = CreateTable("Parça Numarası", "Seri Numarası", "Adet");
        table.SerialQuantityGrouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = table.Columns[0].Id,
            SerialNumberColumnId = table.Columns[1].Id,
            QuantityColumnId = table.Columns[2].Id,
            WasAutoDetected = false
        };
        table.Columns[0].Header = "Yeniden Adlandırılmış PN";

        var diagnosis = service.Diagnose(table);

        Assert.True(diagnosis.IsConfigured);
        Assert.Same(table.Columns[0], diagnosis.MatchKeyColumn);
        Assert.Same(table.Columns[1], diagnosis.SerialColumn);
        Assert.Same(table.Columns[2], diagnosis.QuantityColumn);
        Assert.Contains("Yeniden Adlandırılmış PN", diagnosis.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualGroupingApply_PersistsStableColumnIds()
    {
        var service = CreateGroupingService();
        var table = CreateTable("PN", "Serial No", "Quantity");

        var result = service.ApplyManual(table, table.Columns[0].Id, table.Columns[1].Id, table.Columns[2].Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(table.SerialQuantityGrouping);
        Assert.Equal(table.Columns[0].Id, table.SerialQuantityGrouping!.MatchKeyColumnId);
        Assert.Equal(table.Columns[1].Id, table.SerialQuantityGrouping.SerialNumberColumnId);
        Assert.Equal(table.Columns[2].Id, table.SerialQuantityGrouping.QuantityColumnId);
        Assert.False(table.SerialQuantityGrouping.WasAutoDetected);
    }

    [Fact]
    public void ManualGroupingApply_RejectsDuplicateRoleColumns()
    {
        var service = CreateGroupingService();
        var table = CreateTable("PN", "Serial No", "Quantity");

        var result = service.ApplyManual(table, table.Columns[0].Id, table.Columns[0].Id, table.Columns[2].Id);

        Assert.True(result.IsFailure);
        Assert.Null(table.SerialQuantityGrouping);
    }

    [Fact]
    public void AutoDetectFailure_DoesNotDestroyValidExistingConfig()
    {
        var service = CreateGroupingService();
        var table = CreateTable("Custom Key", "Custom Serial", "Custom Qty");
        var existing = new SerialQuantityGrouping
        {
            MatchKeyColumnId = table.Columns[0].Id,
            SerialNumberColumnId = table.Columns[1].Id,
            QuantityColumnId = table.Columns[2].Id,
            WasAutoDetected = false
        };
        table.SerialQuantityGrouping = existing;

        var result = service.AutoDetect(table);

        Assert.True(result.IsFailure);
        Assert.Same(existing, table.SerialQuantityGrouping);
    }

    [Fact]
    public void RemoveGrouping_SetsConfigurationNull()
    {
        var service = CreateGroupingService();
        var table = CreateTable("PN", "Serial No", "Quantity");
        Assert.True(service.AutoDetect(table).IsSuccess);

        var result = service.Remove(table);

        Assert.True(result.IsSuccess);
        Assert.Null(table.SerialQuantityGrouping);
    }

    [Fact]
    public void TableFormatSelection_PersistsKeyWithoutChangingRowsOrGrouping()
    {
        var selectionService = new TableFormatSelectionService();
        var table = CreateTable("PN", "Serial No", "Quantity");
        var row = new TableRow();
        table.Rows.Add(row);
        var grouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = table.Columns[0].Id,
            SerialNumberColumnId = table.Columns[1].Id,
            QuantityColumnId = table.Columns[2].Id,
            WasAutoDetected = true
        };
        table.SerialQuantityGrouping = grouping;

        var result = selectionService.Apply(table, CreateProfile(), "table-2");

        Assert.True(result.IsSuccess);
        Assert.Equal("table-2", table.ReferenceTableFormatKey);
        Assert.Single(table.Rows);
        Assert.Same(row, table.Rows[0]);
        Assert.Same(grouping, table.SerialQuantityGrouping);
    }

    private static SerialQuantityGroupingConfigurationService CreateGroupingService() =>
        new(new SerialQuantityGroupingDetector());

    private static TableElement CreateTable(params string[] headers)
    {
        var table = new TableElement { Name = "Test" };
        foreach (var header in headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });
        return table;
    }

    private static DocumentFormatProfile CreateProfile() => new()
    {
        Page = new PageFormatProfile
        {
            WidthMillimeters = 210d,
            HeightMillimeters = 297d,
            MarginTopMillimeters = 25d,
            MarginBottomMillimeters = 25d,
            MarginLeftMillimeters = 25d,
            MarginRightMillimeters = 25d,
            HeaderDistanceMillimeters = 12.49d,
            FooterDistanceMillimeters = 12.49d
        },
        PrimaryHeading = TextFormat(12d, bold: true, italic: true, alignment: ParagraphAlignment.Left, before: 4d, after: 2d, keep: true),
        SecondaryHeading = TextFormat(12d, bold: true, alignment: ParagraphAlignment.Left, leftIndent: 6.5d, keep: true),
        BodyText = TextFormat(10d, alignment: ParagraphAlignment.Justify, lineSpacing: 1.15d),
        TableCaption = TextFormat(10d, bold: true, alignment: ParagraphAlignment.Center, keep: true),
        TableCaptionSequence = null,
        TableFormats =
        [
            TableProfile("table-1", "Referans Tablo 1", 6, 100d, 1d),
            TableProfile("table-2", "Referans Tablo 2", 3, 99.32d, 4d)
        ],
        Warnings = []
    };

    private static ResolvedTextFormat TextFormat(
        double size,
        bool bold = false,
        bool italic = false,
        ParagraphAlignment alignment = ParagraphAlignment.Left,
        double before = 0d,
        double after = 0d,
        double lineSpacing = 1d,
        double leftIndent = 0d,
        bool keep = false) => new()
    {
        FontFamilyName = "Arial",
        FontSizePoints = size,
        Bold = bold,
        Italic = italic,
        Underline = false,
        ForegroundColor = "#FF000000",
        Alignment = alignment,
        SpaceBeforePoints = before,
        SpaceAfterPoints = after,
        LineSpacingMultiple = lineSpacing,
        LeftIndentMillimeters = leftIndent,
        FirstLineIndentMillimeters = 0d,
        KeepWithNext = keep
    };

    private static ReferenceTableFormatProfile TableProfile(
        string key,
        string name,
        int columnCount,
        double widthPercent,
        double firstWeight)
    {
        var columns = Enumerable.Range(0, columnCount)
            .Select(index => new ResolvedTableColumnFormat
            {
                WidthWeight = index == 0 ? firstWeight : 1d,
                HeaderAlignment = ParagraphAlignment.Center,
                BodyAlignment = ParagraphAlignment.Left,
                HeaderFontFamilyName = "Arial",
                HeaderFontSizePoints = 10d,
                HeaderBold = true,
                BodyFontFamilyName = "Arial",
                BodyFontSizePoints = 10d,
                BodyBold = false,
                VerticalAlignment = VerticalContentAlignment.Center,
                NoWrap = false
            })
            .ToArray();

        return new ReferenceTableFormatProfile
        {
            Key = key,
            DisplayName = name,
            ReferenceHeaders = Enumerable.Range(1, columnCount).Select(index => $"C{index}").ToArray(),
            Format = new ResolvedTableFormat
            {
                WidthPercent = widthPercent,
                FixedLayout = true,
                BorderSizePoints = 0.5d,
                CellMarginTopMillimeters = 0d,
                CellMarginBottomMillimeters = 0d,
                CellMarginLeftMillimeters = 1.235d,
                CellMarginRightMillimeters = 1.235d,
                PreferredRowHeightMillimeters = 10.195d,
                RepeatHeader = true,
                Columns = columns
            }
        };
    }
}
