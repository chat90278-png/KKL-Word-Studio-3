namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

public class Sprint10TransferSourceMappingTests
{
    [Fact]
    public void AdditionalSource_UnresolvedSchema_RequiresExplicitMappingWithoutMutatingTableSources()
    {
        var (project, report, table) = CreateLegacyTable();
        var service = new ExcelReportTransferService();
        var request = BuildAdditionalSourceRequest(table, "Engine Label", appliedMappings: null);

        var result = service.Transfer(project, report, request);

        Assert.Equal(TransferOutcome.RequiresSourceFieldMapping, result.Outcome);
        Assert.Empty(table.Sources);
        Assert.NotNull(table.Binding);
        var requirement = Assert.Single(result.SourceFieldMappingRequirements);
        Assert.Equal(table.Columns[0].Id, requirement.TableColumnId);
        Assert.Null(requirement.SuggestedSourceField);
        Assert.Contains(requirement.AvailableSourceFields, option => option.SourceField == "A");
    }

    [Fact]
    public void AdditionalSource_ExplicitFieldMapping_PersistsProviderFieldByStableTableColumnId()
    {
        var (project, report, table) = CreateLegacyTable();
        var service = new ExcelReportTransferService();
        var firstAttempt = service.Transfer(project, report, BuildAdditionalSourceRequest(table, "Engine Label", appliedMappings: null));
        Assert.Equal(TransferOutcome.RequiresSourceFieldMapping, firstAttempt.Outcome);

        var request = BuildAdditionalSourceRequest(table, "Engine Label", appliedMappings: null);
        request = new ExcelTransferRequest
        {
            WorkbookFilePath = request.WorkbookFilePath,
            WorkbookFileName = request.WorkbookFileName,
            WorksheetName = request.WorksheetName,
            Range = request.Range,
            HeaderTexts = request.HeaderTexts,
            TargetElementId = request.TargetElementId,
            ExistingTableMode = request.ExistingTableMode,
            SourceFieldMappings = new[]
            {
                new TransferSourceFieldMapping { TableColumnId = table.Columns[0].Id, SourceField = "A" }
            }
        };

        var result = service.Transfer(project, report, request);

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Equal(2, table.Sources.Count);
        var added = table.Sources[1];
        var mapping = Assert.Single(added.FieldMappings);
        Assert.Equal(table.Columns[0].Id, mapping.TableColumnId);
        Assert.Equal("A", mapping.SourceField);
        Assert.Equal("Özel Ad", table.Columns[0].Header);
    }

    private static (Project Project, Report Report, TableElement Table) CreateLegacyTable()
    {
        var project = new Project { Name = "Project" };
        var source = new ExcelDataSource
        {
            Name = "Parts",
            Workbook = new Workbook { FileName = "Parts.xlsx", SourcePath = "/tmp/Parts.xlsx" },
            ActiveWorksheetName = "Sheet1"
        };
        var worksheet = new Worksheet
        {
            Name = "Sheet1",
            SelectedRange = new DataRange
            {
                HeaderRowIndex = 1,
                DataStartRow = 2,
                DataEndRow = 3,
                StartColumn = 1,
                EndColumn = 1
            }
        };
        worksheet.ColumnMappings.Add(new ColumnMapping
        {
            SourceColumn = "A",
            TargetField = new DataField { Name = "PartName", DataType = "Text" }
        });
        source.Workbook.Worksheets.Add(worksheet);
        project.DataSources.Add(source);

        var report = new Report { Name = "Report" };
        var page = new Page();
        var section = new Section { Kind = SectionKind.Body };
        var table = new TableElement
        {
            Name = "Table",
            Binding = new Binding { DataSourceName = source.Name, WorksheetName = worksheet.Name }
        };
        table.Columns.Add(new TableColumn { Header = "Özel Ad", SourceField = "PartName" });
        section.Root.Children.Add(table);
        page.Sections.Add(section);
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report, table);
    }

    private static ExcelTransferRequest BuildAdditionalSourceRequest(
        TableElement table,
        string header,
        IReadOnlyList<TransferColumnMapping>? appliedMappings) => new()
    {
        WorkbookFilePath = "/tmp/Engines.xlsx",
        WorkbookFileName = "Engines.xlsx",
        WorksheetName = "Data",
        Range = new DataRange
        {
            HeaderRowIndex = 1,
            DataStartRow = 2,
            DataEndRow = 3,
            StartColumn = 1,
            EndColumn = 1
        },
        HeaderTexts = new[] { header },
        AppliedColumnMappings = appliedMappings,
        TargetElementId = table.Id,
        ExistingTableMode = ExistingTableTransferMode.AddAsSource
    };
}
