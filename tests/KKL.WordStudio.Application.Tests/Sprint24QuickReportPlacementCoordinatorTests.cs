namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public sealed class Sprint24QuickReportPlacementCoordinatorTests
{
    [Fact]
    public void HeadingAnchor_CreatesRealAltHeadingAndTableUnderIt()
    {
        var (project, report, body) = CreateProjectAndReport();
        var heading = Heading("Parent");
        body.Root.Children.Add(heading);

        var result = ExcelTransferPlacementCoordinator.Transfer(
            new ExcelReportTransferService(),
            project,
            report,
            Placement(
                heading.Id,
                ExcelTransferPlacementAnchorKind.Heading,
                includeHeading: false,
                includeAltHeading: true));

        Assert.Equal(TransferOutcome.Success, result.TransferResult.Outcome);
        Assert.Equal(2, result.CreatedElementIds.Count);
        var alt = Assert.IsType<TextElement>(body.Root.Children.Single(element =>
            element.Id == result.CreatedElementIds[0]));
        Assert.True(HeadingStylePresets.IsAltHeading(alt.Style));
        Assert.Equal("Alt Heading", alt.Name);
        Assert.Equal(alt.Id, body.Root.Children[body.Root.Children.IndexOf(alt)].Id);
        Assert.Equal(result.TransferResult.Table!.Id, result.CreatedElementIds[1]);
    }

    [Fact]
    public void AltHeadingAnchor_WithBothRowsDisabled_AppendsOnlyTableToThatBlock()
    {
        var (project, report, body) = CreateProjectAndReport();
        var heading = Heading("Parent");
        var alt = AltHeading("Existing detail");
        var existingTable = new TableElement { Name = "Existing" };
        var nextHeading = Heading("Next");
        body.Root.Children.Add(heading);
        body.Root.Children.Add(alt);
        body.Root.Children.Add(existingTable);
        body.Root.Children.Add(nextHeading);

        var result = ExcelTransferPlacementCoordinator.Transfer(
            new ExcelReportTransferService(),
            project,
            report,
            Placement(
                alt.Id,
                ExcelTransferPlacementAnchorKind.AltHeading,
                includeHeading: false,
                includeAltHeading: false));

        Assert.Equal(TransferOutcome.Success, result.TransferResult.Outcome);
        Assert.Single(result.CreatedElementIds);
        var createdTable = Assert.IsType<TableElement>(result.TransferResult.Table);
        Assert.True(body.Root.Children.IndexOf(createdTable) > body.Root.Children.IndexOf(existingTable));
        Assert.True(body.Root.Children.IndexOf(createdTable) < body.Root.Children.IndexOf(nextHeading));
    }

    [Fact]
    public void WrongAnchorLevel_FailsWithoutCreatingStructure()
    {
        var (project, report, body) = CreateProjectAndReport();
        var alt = AltHeading("Wrong level");
        body.Root.Children.Add(alt);
        var beforeCount = body.Root.Children.Count;

        var result = ExcelTransferPlacementCoordinator.Transfer(
            new ExcelReportTransferService(),
            project,
            report,
            Placement(
                alt.Id,
                ExcelTransferPlacementAnchorKind.Heading,
                includeHeading: false,
                includeAltHeading: true));

        Assert.Equal(TransferOutcome.Failed, result.TransferResult.Outcome);
        Assert.Equal(beforeCount, body.Root.Children.Count);
        Assert.Empty(result.CreatedElementIds);
    }

    private static ExcelTransferPlacementRequest Placement(
        Guid anchorId,
        ExcelTransferPlacementAnchorKind requiredKind,
        bool includeHeading,
        bool includeAltHeading) => new()
    {
        Transfer = new ExcelTransferRequest
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
                EndColumn = 1
            },
            HeaderTexts = ["No"],
            AppliedColumnMappings =
            [
                new TransferColumnMapping
                {
                    SourceColumn = "A",
                    FieldName = "ItemNumber",
                    DataType = "string"
                }
            ],
            TargetElementId = anchorId
        },
        DestinationMode = ExcelTransferDestinationMode.CreateNewTable,
        AnchorElementId = anchorId,
        RequiredAnchorKind = requiredKind,
        TableName = "New table",
        IncludeHeading = includeHeading,
        HeadingText = "New heading",
        IncludeAltHeading = includeAltHeading,
        AltHeadingText = "New detail",
        Columns =
        [
            new TransferColumnSelection
            {
                ProviderField = "A",
                LogicalField = "ItemNumber",
                Header = "No",
                SemanticRole = ExcelSemanticFieldRole.ItemNumber,
                SourceOrder = 0,
                IsIncluded = true
            }
        ]
    };

    private static (Project Project, Report Report, Section Body) CreateProjectAndReport()
    {
        var project = new Project { Name = "Project" };
        var report = new Report { Name = "Report" };
        var page = new Page();
        var body = new Section { Name = "Body", Kind = SectionKind.Body, AutoHeight = true };
        body.Root.Children.Add(new TextElement
        {
            Name = "Document Root",
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal("Root")
        });
        page.Sections.Add(body);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report, body);
    }

    private static TextElement Heading(string text) => new()
    {
        Name = "Heading",
        Style = HeadingStylePresets.CreateHeadingStyle(),
        Content = Expression.Literal(text)
    };

    private static TextElement AltHeading(string text) => new()
    {
        Name = "Alt Heading",
        Style = HeadingStylePresets.CreateAltHeadingStyle(),
        Content = Expression.Literal(text)
    };
}
