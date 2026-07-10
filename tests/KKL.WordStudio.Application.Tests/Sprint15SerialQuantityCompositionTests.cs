namespace KKL.WordStudio.Application.Tests;

using System.Globalization;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint15SerialQuantityCompositionTests
{
    private readonly SerialQuantityGroupingDetector detector = new();
    private readonly SerialQuantityTableContentRowComposer composer = new();

    [Fact]
    public void Detector_DetectsEnglishProductSerialQuantityAliases()
    {
        var table = CreateTable("Product Number", "Serial Number", "Qty");

        var grouping = detector.Detect(table.Columns);

        Assert.NotNull(grouping);
        Assert.Equal(table.Columns[0].Id, grouping!.MatchKeyColumnId);
        Assert.Equal(table.Columns[1].Id, grouping.SerialNumberColumnId);
        Assert.Equal(table.Columns[2].Id, grouping.QuantityColumnId);
        Assert.True(grouping.WasAutoDetected);
    }

    [Fact]
    public void Detector_DetectsTurkishAliases()
    {
        var table = CreateTable("Parça Numarası", "Seri Numarası", "Adet");

        var grouping = detector.Detect(table.Columns);

        Assert.NotNull(grouping);
        Assert.Equal(table.Columns[0].Id, grouping!.MatchKeyColumnId);
        Assert.Equal(table.Columns[1].Id, grouping.SerialNumberColumnId);
        Assert.Equal(table.Columns[2].Id, grouping.QuantityColumnId);
    }

    [Fact]
    public void Detector_AmbiguousRole_DoesNotGuess()
    {
        var table = CreateTable("PN", "Serial No", "S/N", "Quantity");

        Assert.Null(detector.Detect(table.Columns));
    }


    [Fact]
    public void Detector_UsesSourceFieldAliasesWhenHeadersAreRenamed()
    {
        var table = CreateTable("Parça", "Seri", "Sayı");
        table.Columns[0].SourceField = "Product No";
        table.Columns[1].SourceField = "Serial No";
        table.Columns[2].SourceField = "Quantity";

        var grouping = detector.Detect(table.Columns);

        Assert.NotNull(grouping);
        Assert.Equal(table.Columns[0].Id, grouping!.MatchKeyColumnId);
        Assert.Equal(table.Columns[1].Id, grouping.SerialNumberColumnId);
        Assert.Equal(table.Columns[2].Id, grouping.QuantityColumnId);
    }

    [Fact]
    public void Detector_AmbiguousQuantityRole_DoesNotGuess()
    {
        var table = CreateTable("PN", "Serial No", "Quantity", "Qty");

        Assert.Null(detector.Detect(table.Columns));
    }

    [Fact]
    public void Transfer_NewTable_PersistsStableGroupingColumnIds()
    {
        var (project, report) = CreateProjectAndReport();

        var result = new ExcelReportTransferService().Transfer(
            project,
            report,
            BuildTransferRequest(headers: ["Product No", "Serial No", "Quantity"]));

        var table = Assert.IsType<TableElement>(result.Table);
        Assert.NotNull(table.SerialQuantityGrouping);
        Assert.Equal(table.Columns[0].Id, table.SerialQuantityGrouping!.MatchKeyColumnId);
        Assert.Equal(table.Columns[1].Id, table.SerialQuantityGrouping.SerialNumberColumnId);
        Assert.Equal(table.Columns[2].Id, table.SerialQuantityGrouping.QuantityColumnId);
        Assert.True(table.SerialQuantityGrouping.WasAutoDetected);
    }

    [Fact]
    public void Transfer_HeaderRenameAfterDetection_DoesNotChangeGroupingIds()
    {
        var (project, report) = CreateProjectAndReport();
        var result = new ExcelReportTransferService().Transfer(
            project,
            report,
            BuildTransferRequest(headers: ["Product No", "Serial No", "Quantity"]));
        var table = Assert.IsType<TableElement>(result.Table);
        var grouping = Assert.IsType<SerialQuantityGrouping>(table.SerialQuantityGrouping);
        var ids = (grouping.MatchKeyColumnId, grouping.SerialNumberColumnId, grouping.QuantityColumnId);

        table.Columns[0].Header = "Parça Numarası";
        table.Columns[1].Header = "Seri";
        table.Columns[2].Header = "Miktar Gösterimi";

        Assert.Equal(ids.MatchKeyColumnId, table.SerialQuantityGrouping!.MatchKeyColumnId);
        Assert.Equal(ids.SerialNumberColumnId, table.SerialQuantityGrouping.SerialNumberColumnId);
        Assert.Equal(ids.QuantityColumnId, table.SerialQuantityGrouping.QuantityColumnId);
    }

    [Fact]
    public void Transfer_AddSource_PreservesExistingGrouping()
    {
        var (project, report) = CreateProjectAndReport();
        var table = CreateTable("Product No", "Serial No", "Quantity");
        report.Pages[0].Sections[0].Root.Children.Add(table);
        table.SerialQuantityGrouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = table.Columns[0].Id,
            SerialNumberColumnId = table.Columns[1].Id,
            QuantityColumnId = table.Columns[2].Id,
            WasAutoDetected = false
        };
        table.Sources.Add(new TableSourceBinding
        {
            DataSourceName = "Existing",
            WorksheetName = "Sheet1",
            Range = Range(1, 3)
        });
        var grouping = table.SerialQuantityGrouping;

        var result = new ExcelReportTransferService().Transfer(
            project,
            report,
            BuildTransferRequest(table.Id, ExistingTableTransferMode.AddAsSource, ["Product No", "Serial No", "Quantity"]));

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Same(grouping, table.SerialQuantityGrouping);
        Assert.False(table.SerialQuantityGrouping!.WasAutoDetected);
    }

    [Fact]
    public void Transfer_RebindKeepColumns_PreservesValidGroupingIds()
    {
        var (project, report) = CreateProjectAndReport();
        var table = CreateTable("PN", "S/N", "Qty");
        table.Binding = new Binding { DataSourceName = "Old", WorksheetName = "OldSheet" };
        table.SerialQuantityGrouping = detector.Detect(table.Columns);
        report.Pages[0].Sections[0].Root.Children.Add(table);
        var grouping = table.SerialQuantityGrouping;

        var result = new ExcelReportTransferService().Transfer(
            project,
            report,
            BuildTransferRequest(table.Id, ExistingTableTransferMode.RebindKeepColumns, ["Different A", "Different B", "Different C"]));

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Same(grouping, table.SerialQuantityGrouping);
    }


    [Fact]
    public void Transfer_RebindKeepColumns_DetectsWhenGroupingIsMissing()
    {
        var (project, report) = CreateProjectAndReport();
        var table = CreateTable("PN", "S/N", "Qty");
        table.Binding = new Binding { DataSourceName = "Old", WorksheetName = "OldSheet" };
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var result = new ExcelReportTransferService().Transfer(
            project,
            report,
            BuildTransferRequest(table.Id, ExistingTableTransferMode.RebindKeepColumns, ["Different A", "Different B", "Different C"]));

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.NotNull(table.SerialQuantityGrouping);
        Assert.Equal(table.Columns[0].Id, table.SerialQuantityGrouping!.MatchKeyColumnId);
        Assert.Equal(table.Columns[1].Id, table.SerialQuantityGrouping.SerialNumberColumnId);
        Assert.Equal(table.Columns[2].Id, table.SerialQuantityGrouping.QuantityColumnId);
    }

    [Fact]
    public void Transfer_AddSource_DetectsWhenGroupingIsMissing()
    {
        var (project, report) = CreateProjectAndReport();
        var table = CreateTable("Product No", "Serial No", "Quantity");
        report.Pages[0].Sections[0].Root.Children.Add(table);
        table.Sources.Add(new TableSourceBinding
        {
            DataSourceName = "Existing",
            WorksheetName = "Sheet1",
            Range = Range(1, 3)
        });

        var result = new ExcelReportTransferService().Transfer(
            project,
            report,
            BuildTransferRequest(table.Id, ExistingTableTransferMode.AddAsSource, ["Product No", "Serial No", "Quantity"]));

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.NotNull(table.SerialQuantityGrouping);
        Assert.True(table.SerialQuantityGrouping!.WasAutoDetected);
    }

    [Fact]
    public void Transfer_ReplaceColumns_RedetectsAndClearsAmbiguousGrouping()
    {
        var (project, report) = CreateProjectAndReport();
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");
        table.Binding = new Binding { DataSourceName = "Old", WorksheetName = "OldSheet" };
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var result = new ExcelReportTransferService().Transfer(
            project,
            report,
            BuildTransferRequest(table.Id, ExistingTableTransferMode.ReplaceColumnsFromSource, ["PN", "Serial No", "S/N"]));

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Null(table.SerialQuantityGrouping);
    }

    [Fact]
    public void Composer_NoConfiguration_PassesRowsThrough()
    {
        var table = CreateTable("PN", "Serial No", "Quantity");
        IReadOnlyList<IReadOnlyList<string>> rows = [["1234", "A222", "2"]];

        var result = composer.Compose(table, rows);

        Assert.Same(rows, result.Rows);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Composer_Quantity2AndTwoSerials_ExpandsRowsAndSpansNonSerialCells()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["1234", "A222", "2"],
            ["1234", "A221", "2"]
        ]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(new[] { "1234", "A222", "2" }, result.Rows[0]);
        Assert.Equal(new[] { "", "A221", "" }, result.Rows[1]);
        Assert.Equal(2, result.CellSpans.Count);
        Assert.Contains(result.CellSpans, span => span is { RowIndex: 0, ColumnIndex: 0, RowSpan: 2 });
        Assert.Contains(result.CellSpans, span => span is { RowIndex: 0, ColumnIndex: 2, RowSpan: 2 });
        Assert.DoesNotContain(result.CellSpans, span => span.ColumnIndex == 1);
        var group = Assert.Single(result.RowGroups);
        Assert.Equal(0, group.StartRowIndex);
        Assert.Equal(2, group.RowCount);
        Assert.True(group.KeepTogetherWhenPossible);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Composer_Quantity3AndThreeSerials_CreatesThreeRowGroup()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["1234", "A1", "3"],
            ["1234", "A2", "3"],
            ["1234", "A3", "3"]
        ]);

        Assert.Equal(3, result.Rows.Count);
        var group = Assert.Single(result.RowGroups);
        Assert.Equal(3, group.RowCount);
        Assert.All(result.CellSpans, span => Assert.Equal(3, span.RowSpan));
    }


    [Fact]
    public void Composer_WholeDecimalQuantity_PreservesFirstCanonicalText()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["1234", "A1", "2.0"],
            ["1234", "A2", "2"]
        ]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("2.0", result.Rows[0][2]);
        Assert.Single(result.RowGroups);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Composer_DecimalCommaQuantity_IsAcceptedWhenCurrentCultureUsesComma()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

            var result = composer.Compose(table,
            [
                ["1234", "A1", "2,0"],
                ["1234", "A2", "2,0"]
            ]);

            Assert.Equal(2, result.Rows.Count);
            Assert.Equal("2,0", result.Rows[0][2]);
            Assert.Single(result.RowGroups);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2.5")]
    public void Composer_NonPositiveOrFractionalQuantity_PreservesRowsAndWarns(string quantity)
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");
        IReadOnlyList<IReadOnlyList<string>> rows = [["1234", "A1", quantity]];

        var result = composer.Compose(table, rows);

        Assert.Single(result.Rows);
        Assert.Equal(quantity, result.Rows[0][2]);
        Assert.Empty(result.RowGroups);
        Assert.Contains("geçersiz Adet", Assert.Single(result.Warnings));
    }

    [Fact]
    public void Composer_SerialCell_DoesNotSplitCommaOrSemicolon()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table, [["1234", "A1,A2;A3", "3"]]);

        Assert.Single(result.Rows);
        Assert.Equal("A1,A2;A3", result.Rows[0][1]);
        Assert.Empty(result.RowGroups);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Composer_Quantity100AndNoSerial_ProducesOneRow()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table, [["1234", "", "100"]]);

        Assert.Single(result.Rows);
        Assert.Empty(result.RowGroups);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Composer_Quantity100AndOneSerial_ProducesOneRow()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table, [["1234", "A1", "100"]]);

        Assert.Single(result.Rows);
        Assert.Equal("A1", result.Rows[0][1]);
        Assert.Empty(result.RowGroups);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Composer_Quantity3AndTwoSerials_AggregatesSerialCellAndWarns()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["1234", "A1", "3"],
            ["1234", "A2", "3"]
        ]);

        Assert.Single(result.Rows);
        Assert.Equal("A1\nA2", result.Rows[0][1]);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Contains("Adet 3", Assert.Single(result.Warnings));
        Assert.Contains("Seri No 2", result.Warnings[0]);
    }

    [Fact]
    public void Composer_DuplicateSerials_DoNotCreateGroupedLayout()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["1234", "A1", "2"],
            ["1234", "a1", "2"]
        ]);

        Assert.Single(result.Rows);
        Assert.Equal("A1\na1", result.Rows[0][1]);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Contains("tekrarlanan", Assert.Single(result.Warnings), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Composer_ConflictingQuantity_PreservesOriginalRowsAndWarns()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["1234", "A1", "2"],
            ["1234", "A2", "3"]
        ];

        var result = composer.Compose(table, rows);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(rows[0], result.Rows[0]);
        Assert.Equal(rows[1], result.Rows[1]);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Contains("çelişkili Adet", Assert.Single(result.Warnings));
    }

    [Fact]
    public void Composer_ConflictingProductData_PreservesOriginalRowsAndWarns()
    {
        var table = CreateConfiguredTable("Product Name", "Product No", "Serial No", "Quantity");
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["Elma", "1234", "A1", "2"],
            ["Armut", "1234", "A2", "2"]
        ];

        var result = composer.Compose(table, rows);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("Elma", result.Rows[0][0]);
        Assert.Equal("Armut", result.Rows[1][0]);
        Assert.Empty(result.RowGroups);
        Assert.Contains("Product Name", Assert.Single(result.Warnings));
    }


    [Fact]
    public void Composer_QuantityAndProductConflicts_ReportBothDataQualityWarnings()
    {
        var table = CreateConfiguredTable("Product Name", "PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["Elma", "1234", "A1", "2"],
            ["Armut", "1234", "A2", "3"]
        ]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Empty(result.RowGroups);
        Assert.Contains(result.Warnings, warning => warning.Contains("çelişkili Adet", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("Product Name", StringComparison.Ordinal));
    }

    [Fact]
    public void Composer_EmptyKeys_AreNeverGroupedTogether()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["", "A1", "2"],
            ["", "A2", "2"]
        ]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("A1", result.Rows[0][1]);
        Assert.Equal("A2", result.Rows[1][1]);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Composer_FirstOccurrenceOrder_IsPreserved()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["X", "A1", "2"],
            ["", "BLANK", "9"],
            ["X", "A2", "2"],
            ["Y", "Y1", "1"]
        ]);

        Assert.Equal(new[] { "A1", "A2", "BLANK", "Y1" }, result.Rows.Select(row => row[1]));
    }

    [Fact]
    public void Composer_MultiSourceNormalizedRows_CanCompleteOneSerialGroup()
    {
        var table = CreateConfiguredTable("Product No", "Serial No", "Quantity");
        IReadOnlyList<IReadOnlyList<string>> sourceAAndBNormalizedRows =
        [
            ["1234", "A222", "2"],
            ["1234", "A221", "2"]
        ];

        var result = composer.Compose(table, sourceAAndBNormalizedRows);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("A222", result.Rows[0][1]);
        Assert.Equal("A221", result.Rows[1][1]);
        Assert.Single(result.RowGroups);
    }

    [Fact]
    public void Composer_NewlineSeparatedSerialCell_CanFormExactGroup()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table, [["1234", " A222\r\n\nA221 ", "2"]]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("A222", result.Rows[0][1]);
        Assert.Equal("A221", result.Rows[1][1]);
        Assert.Single(result.RowGroups);
    }

    [Fact]
    public void Composer_MalformedQuantity_PreservesRowsAndWarns()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["1234", "A1", "abc"],
            ["1234", "A2", ""]
        ];

        var result = composer.Compose(table, rows);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("abc", result.Rows[0][2]);
        Assert.Equal("", result.Rows[1][2]);
        Assert.Empty(result.RowGroups);
        Assert.Contains("geçersiz Adet", Assert.Single(result.Warnings));
    }

    [Fact]
    public void Composer_OutputSpans_UseExpandedRowIndexes()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["", "Independent", "7"],
            ["X", "X1", "2"],
            ["Y", "Y1", "2"],
            ["X", "X2", "2"],
            ["Y", "Y2", "2"]
        ]);

        Assert.Equal(new[] { 1, 3 }, result.RowGroups.Select(group => group.StartRowIndex));
        Assert.Contains(result.CellSpans, span => span is { RowIndex: 1, ColumnIndex: 0, RowSpan: 2 });
        Assert.Contains(result.CellSpans, span => span is { RowIndex: 3, ColumnIndex: 0, RowSpan: 2 });
        Assert.All(result.CellSpans, span => Assert.InRange(span.RowIndex, 0, result.Rows.Count - 1));
    }

    [Fact]
    public void Composer_OrdinalColumnMayUseFirstNonEmptyValue()
    {
        var table = CreateConfiguredTable("No.", "PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["7", "X", "X1", "2"],
            ["8", "X", "X2", "2"]
        ]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("7", result.Rows[0][0]);
        Assert.Single(result.RowGroups);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Composer_MissingCellsArePaddedAndExtraCellsAreIgnoredWithoutShifting()
    {
        var table = CreateConfiguredTable("PN", "Serial No", "Quantity");

        var result = composer.Compose(table,
        [
            ["X", "X1", "2", "EXTRA"],
            ["X", "X2"]
        ]);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(3, result.Rows[0].Count);
        Assert.Equal("2", result.Rows[0][2]);
        Assert.Equal("", result.Rows[1][2]);
        Assert.Single(result.RowGroups);
    }

    [Fact]
    public void Composer_InvalidConfiguredIds_PassesRowsThroughAndWarns()
    {
        var table = CreateTable("PN", "Serial No", "Quantity");
        table.SerialQuantityGrouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = Guid.NewGuid(),
            SerialNumberColumnId = table.Columns[1].Id,
            QuantityColumnId = table.Columns[2].Id
        };
        IReadOnlyList<IReadOnlyList<string>> rows = [["X", "X1", "1", "EXTRA"]];

        var result = composer.Compose(table, rows);

        Assert.Same(rows, result.Rows);
        Assert.Empty(result.CellSpans);
        Assert.Empty(result.RowGroups);
        Assert.Single(result.Warnings);
    }

    private TableElement CreateConfiguredTable(params string[] headers)
    {
        var table = CreateTable(headers);
        table.SerialQuantityGrouping = detector.Detect(table.Columns);
        Assert.NotNull(table.SerialQuantityGrouping);
        return table;
    }

    private static TableElement CreateTable(params string[] headers)
    {
        var table = new TableElement { Name = "Table" };
        foreach (var header in headers)
            table.Columns.Add(new TableColumn { Header = header, SourceField = header });
        return table;
    }

    private static (Project Project, Report Report) CreateProjectAndReport()
    {
        var project = new Project { Name = "Project" };
        var report = new Report { Name = "Report" };
        var page = new Page();
        page.Sections.Add(new Section { Name = "Body", Kind = SectionKind.Body });
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report);
    }

    private static ExcelTransferRequest BuildTransferRequest(
        Guid? targetElementId = null,
        ExistingTableTransferMode? mode = null,
        IReadOnlyList<string>? headers = null) => new()
    {
        WorkbookFilePath = "/tmp/Sprint15.xlsx",
        WorkbookFileName = "Sprint15.xlsx",
        WorksheetName = "Sheet1",
        Range = Range(1, 3),
        HeaderTexts = headers ?? ["Product No", "Serial No", "Quantity"],
        TargetElementId = targetElementId,
        ExistingTableMode = mode
    };

    private static DataRange Range(int startColumn, int endColumn) => new()
    {
        HeaderRowIndex = 1,
        DataStartRow = 2,
        DataEndRow = 4,
        StartColumn = startColumn,
        EndColumn = endColumn
    };
}
