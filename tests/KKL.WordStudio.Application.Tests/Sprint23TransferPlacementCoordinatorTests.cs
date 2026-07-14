namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint23TransferPlacementCoordinatorTests
{
    [Fact]
    public void OrderColumns_UsesCanonicalRoleOrderAndKeepsUnknownsInSourceOrder()
    {
        // Historical test name is retained for baseline-inventory compatibility.
        // The accepted Sprint 23 behavior now preserves selected Excel columns in
        // physical left-to-right source order; semantic roles drive selection and
        // binding identity, not output rearrangement.
        var ordered = ExcelTransferPlacementCoordinator.OrderColumns(
        [
            Column("F", "Quantity", "Qty", ExcelSemanticFieldRole.Quantity, 5),
            Column("B", "UnknownB", "Extra B", ExcelSemanticFieldRole.Unknown, 1),
            Column("E", "ItemNumber", "No", ExcelSemanticFieldRole.ItemNumber, 4),
            Column("A", "UnknownA", "Extra A", ExcelSemanticFieldRole.Unknown, 0),
            Column("C", "SerialNumber", "Serial No", ExcelSemanticFieldRole.SerialNumber, 2),
            Column("D", "PartNumber", "Part No", ExcelSemanticFieldRole.PartNumber, 3)
        ]);

        Assert.Equal(
            ["UnknownA", "UnknownB", "SerialNumber", "PartNumber", "ItemNumber", "Quantity"],
            ordered.Select(column => column.LogicalField));
    }

    [Fact]
    public void CreateNewTable_CreatesRootHeadingAltHeadingAndCanonicalColumns()
    {
        // Historical method name is retained. Columns now follow the active Excel
        // source order and the table name is materialized as a visible text element.
        var (project, report, body) = CreateProjectAndReport();
        var placement = new ExcelTransferPlacementRequest
        {
            Transfer = BuildRequest(),
            DestinationMode = ExcelTransferDestinationMode.CreateNewTable,
            TableName = "Configuration List",
            IncludeHeading = true,
            HeadingText = "Main Assembly",
            IncludeAltHeading = true,
            AltHeadingText = "Installed Parts",
            Columns = ShuffledCanonicalColumns()
        };

        var result = ExcelTransferPlacementCoordinator.Transfer(
            new ExcelReportTransferService(),
            project,
            report,
            placement);

        Assert.Equal(TransferOutcome.Success, result.TransferResult.Outcome);
        Assert.Equal(5, body.Root.Children.Count);
        var root = Assert.IsType<TextElement>(body.Root.Children[0]);
        var heading = Assert.IsType<TextElement>(body.Root.Children[1]);
        var altHeading = Assert.IsType<TextElement>(body.Root.Children[2]);
        var title = Assert.IsType<TextElement>(body.Root.Children[3]);
        var table = Assert.IsType<TableElement>(body.Root.Children[4]);

        Assert.Equal("Document Root", root.Name);
        Assert.Equal(ExcelTransferPlacementCoordinator.DefaultRootHeadingText, root.Content.Text);
        Assert.Equal("Main Assembly", heading.Content.Text);
        Assert.Equal("Installed Parts", altHeading.Content.Text);
        Assert.StartsWith(ExcelTransferPlacementCoordinator.TableTitleElementNamePrefix, title.Name, StringComparison.Ordinal);
        Assert.Equal("Configuration List", title.Content.Text);
        Assert.True(title.Style.Bold);
        Assert.Equal("Configuration List", table.Name);
        Assert.Equal(
            ["Nsn", "Quantity", "SerialNumber", "PartNumber", "ItemNumber", "PartNameEnglish"],
            table.Columns.Select(column => column.SourceField));
        Assert.Equal(
            ["NSN", "Qty", "Serial No", "Part No", "No", "English Part Name"],
            table.Columns.Select(column => column.Header));
    }

    [Fact]
    public void UpdateExistingTable_RenamesAndCanonicalizesWithoutMovingTheTable()
    {
        // Historical method name is retained. Updating creates/updates the visible
        // title immediately before the same table object and preserves source order.
        var (project, report, body) = CreateProjectAndReport();
        var table = new TableElement
        {
            Name = "Tablo 1",
            Binding = new Binding { DataSourceName = "Old", WorksheetName = "OldSheet" }
        };
        table.Columns.Add(new TableColumn { Header = "Old", SourceField = "Old" });
        body.Root.Children.Add(table);

        var placement = new ExcelTransferPlacementRequest
        {
            Transfer = BuildRequest(targetElementId: table.Id),
            DestinationMode = ExcelTransferDestinationMode.UpdateExistingTable,
            ExistingTableId = table.Id,
            TableName = "Updated Table",
            Columns = ShuffledCanonicalColumns()
        };

        var result = ExcelTransferPlacementCoordinator.Transfer(
            new ExcelReportTransferService(),
            project,
            report,
            placement);

        Assert.Equal(TransferOutcome.Success, result.TransferResult.Outcome);
        Assert.Equal(2, body.Root.Children.Count);
        var title = Assert.IsType<TextElement>(body.Root.Children[0]);
        Assert.Same(table, body.Root.Children[1]);
        Assert.Equal("Updated Table", title.Content.Text);
        Assert.Equal("Updated Table", table.Name);
        Assert.Equal("Sheet1", table.Binding?.WorksheetName);
        Assert.Equal("Nsn", table.Columns[0].SourceField);
        Assert.Equal("PartNameEnglish", table.Columns[^1].SourceField);
    }

    [Fact]
    public void CreateNewTable_WhenTransferFails_RollsBackTheProposedStructure()
    {
        var (project, report, body) = CreateProjectAndReport();
        var placement = new ExcelTransferPlacementRequest
        {
            Transfer = BuildRequest(),
            DestinationMode = ExcelTransferDestinationMode.CreateNewTable,
            TableName = "Table",
            IncludeHeading = true,
            HeadingText = "Heading",
            IncludeAltHeading = true,
            AltHeadingText = "Alt",
            Columns = ShuffledCanonicalColumns()
        };

        var result = ExcelTransferPlacementCoordinator.Transfer(
            new FailingTransferService(),
            project,
            report,
            placement);

        Assert.Equal(TransferOutcome.Failed, result.TransferResult.Outcome);
        Assert.Empty(body.Root.Children);
    }

    [Fact]
    public void CreateNewTable_WithNoSelectedColumns_FailsBeforeMutatingReport()
    {
        var (project, report, body) = CreateProjectAndReport();
        var placement = new ExcelTransferPlacementRequest
        {
            Transfer = BuildRequest(),
            DestinationMode = ExcelTransferDestinationMode.CreateNewTable,
            TableName = "Table",
            Columns = ShuffledCanonicalColumns().Select(column => new TransferColumnSelection
            {
                ProviderField = column.ProviderField,
                LogicalField = column.LogicalField,
                Header = column.Header,
                SemanticRole = column.SemanticRole,
                SourceOrder = column.SourceOrder,
                IsIncluded = false
            }).ToList()
        };

        var result = ExcelTransferPlacementCoordinator.Transfer(
            new ExcelReportTransferService(),
            project,
            report,
            placement);

        Assert.Equal(TransferOutcome.Failed, result.TransferResult.Outcome);
        Assert.Empty(body.Root.Children);
        Assert.Empty(project.DataSources);
    }

    private static (Project Project, Report Report, Section Body) CreateProjectAndReport()
    {
        var project = new Project { Name = "Project" };
        var report = new Report { Name = "Report" };
        var page = new Page();
        var body = new Section { Name = "Body", Kind = SectionKind.Body, AutoHeight = true };
        page.Sections.Add(body);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report, body);
    }

    private static ExcelTransferRequest BuildRequest(Guid? targetElementId = null) => new()
    {
        WorkbookFilePath = "/tmp/source.xlsx",
        WorkbookFileName = "source.xlsx",
        WorksheetName = "Sheet1",
        Range = new DataRange
        {
            HeaderRowIndex = 1,
            DataStartRow = 2,
            DataEndRow = 10,
            StartColumn = 1,
            EndColumn = 6
        },
        HeaderTexts = ["NSN", "Qty", "Serial No", "Part No", "No", "English Part Name"],
        AppliedColumnMappings =
        [
            Mapping("A", "Nsn"),
            Mapping("B", "Quantity"),
            Mapping("C", "SerialNumber"),
            Mapping("D", "PartNumber"),
            Mapping("E", "ItemNumber"),
            Mapping("F", "PartNameEnglish")
        ],
        TargetElementId = targetElementId
    };

    private static IReadOnlyList<TransferColumnSelection> ShuffledCanonicalColumns() =>
    [
        Column("A", "Nsn", "NSN", ExcelSemanticFieldRole.Nsn, 0),
        Column("B", "Quantity", "Qty", ExcelSemanticFieldRole.Quantity, 1),
        Column("C", "SerialNumber", "Serial No", ExcelSemanticFieldRole.SerialNumber, 2),
        Column("D", "PartNumber", "Part No", ExcelSemanticFieldRole.PartNumber, 3),
        Column("E", "ItemNumber", "No", ExcelSemanticFieldRole.ItemNumber, 4),
        Column("F", "PartNameEnglish", "English Part Name", ExcelSemanticFieldRole.PartNameEnglish, 5)
    ];

    private static TransferColumnSelection Column(
        string providerField,
        string logicalField,
        string header,
        ExcelSemanticFieldRole role,
        int sourceOrder) => new()
    {
        ProviderField = providerField,
        LogicalField = logicalField,
        Header = header,
        SemanticRole = role,
        SourceOrder = sourceOrder,
        IsIncluded = true
    };

    private static TransferColumnMapping Mapping(string source, string field) => new()
    {
        SourceColumn = source,
        FieldName = field,
        DataType = "string"
    };

    private sealed class FailingTransferService : IExcelReportTransferService
    {
        public ExcelTransferResult Transfer(Project project, Report report, ExcelTransferRequest request) =>
            ExcelTransferResult.Failure("Synthetic failure");
    }
}