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
        // Output order is the live Excel grid order supplied through SourceOrder.
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
        // Historical method name is retained. Heading text is visibly numbered,
        // while the existing heading styles remain the Word/Contents identity.
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
        Assert.Equal(4, body.Root.Children.Count);
        var root = Assert.IsType<TextElement>(body.Root.Children[0]);
        var heading = Assert.IsType<TextElement>(body.Root.Children[1]);
        var altHeading = Assert.IsType<TextElement>(body.Root.Children[2]);
        var table = Assert.IsType<TableElement>(body.Root.Children[3]);

        Assert.Equal("Document Root", root.Name);
        Assert.Equal("1. System Test Procedure Configuration List", root.Content.Text);
        Assert.Equal("1.1 Main Assembly", heading.Content.Text);
        Assert.Equal("1.1.1 Installed Parts", altHeading.Content.Text);
        Assert.Equal("Configuration List", table.Name);
        Assert.Equal("Configuration List", table.Caption);
        Assert.DoesNotContain(body.Root.Children.OfType<TextElement>(), text =>
            text.Name.StartsWith(ExcelTransferPlacementCoordinator.TableTitleElementNamePrefix, StringComparison.Ordinal));
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
        // Historical method name is retained. Updating uses the existing native
        // caption pipeline and removes a title element from earlier Tranche heads.
        var (project, report, body) = CreateProjectAndReport();
        var table = new TableElement
        {
            Name = "Tablo 1",
            Binding = new Binding { DataSourceName = "Old", WorksheetName = "OldSheet" }
        };
        table.Columns.Add(new TableColumn { Header = "Old", SourceField = "Old" });
        var legacyTitle = new TextElement
        {
            Name = ExcelTransferPlacementCoordinator.TableTitleElementNamePrefix + table.Id.ToString("N"),
            Content = Domain.Expressions.Expression.Literal("Old title")
        };
        body.Root.Children.Add(legacyTitle);
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
        Assert.Single(body.Root.Children);
        Assert.Same(table, body.Root.Children[0]);
        Assert.Equal("Updated Table", table.Name);
        Assert.Equal("Updated Table", table.Caption);
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
