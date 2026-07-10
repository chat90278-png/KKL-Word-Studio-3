namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.Transfer;
using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using Xunit;

/// <summary>
/// Covers Sprint 7 acceptance tests 1, 2 and 3 (partially — the worksheet-
/// stability half of test 3 belongs to Infrastructure, see
/// DirectTransferBindingTests) plus the "configured table" decision flow
/// and heading-anchored insertion. Uses a plain Project/Report fixture —
/// this service never itself reads a real .xlsx (that's
/// IExcelWorkbookReader's job); it only orchestrates Domain state from an
/// already-captured <see cref="ExcelTransferRequest"/>.
/// </summary>
public class ExcelReportTransferServiceTests
{
    private static (Project Project, Report Report) CreateProjectAndReport()
    {
        var project = new Project { Name = "Test Project" };
        var report = new Report { Name = "Test Report" };
        var page = new Page();
        page.Sections.Add(new Section { Name = "Body", Kind = SectionKind.Body });
        report.Pages.Add(page);
        project.Reports.Add(report);
        return (project, report);
    }

    private static ExcelTransferRequest BuildRequest(
        Guid? targetElementId = null,
        IReadOnlyList<string>? headerTexts = null,
        IReadOnlyList<TransferColumnMapping>? mappings = null,
        ExistingTableTransferMode? existingTableMode = null) => new()
    {
        WorkbookFilePath = "/tmp/Aircraft.xlsx",
        WorkbookFileName = "Aircraft.xlsx",
        WorksheetName = "Sheet1",
        Range = new DataRange { DataStartRow = 2, DataEndRow = 3, HeaderRowIndex = 1, StartColumn = 1, EndColumn = 2 },
        HeaderTexts = headerTexts ?? new[] { "Ad", "Değer" },
        AppliedColumnMappings = mappings,
        TargetElementId = targetElementId,
        ExistingTableMode = existingTableMode
    };

    /// <summary>Required test 1: transferring into a selected UNBOUND table binds it and builds its columns from the source range.</summary>
    [Fact]
    public void TransferConfiguredWorksheet_ToSelectedUnboundTable_BindsAndBuildsColumns()
    {
        var (project, report) = CreateProjectAndReport();
        var table = new TableElement { Name = "Tablo 1" };
        table.Columns.Add(new TableColumn { Header = "Sütun 1" });
        table.Columns.Add(new TableColumn { Header = "Sütun 2" });
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var service = new ExcelReportTransferService();
        var result = service.Transfer(project, report, BuildRequest(targetElementId: table.Id));

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.False(result.CreatedNewTable);
        Assert.NotNull(table.Binding);
        Assert.Equal("Sheet1", table.Binding!.WorksheetName);
        Assert.Single(project.DataSources);

        // Placeholder columns ("Sütun 1/2") are replaced by the source
        // range's own columns on the first transfer into an unbound table.
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal("Ad", table.Columns[0].Header);
        Assert.Equal("A", table.Columns[0].SourceField);
        Assert.Equal("Değer", table.Columns[1].Header);
        Assert.Equal("B", table.Columns[1].SourceField);
    }

    /// <summary>Required test 2: no column mappings supplied — the Excel header row text becomes the default displayed header, and "Sütun {letter}" is the fallback when a header cell is blank.</summary>
    [Fact]
    public void TransferWithoutColumnMappings_UsesExcelHeaderText()
    {
        var (project, report) = CreateProjectAndReport();
        var table = new TableElement { Name = "Tablo 1" };
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var service = new ExcelReportTransferService();
        var request = BuildRequest(targetElementId: table.Id, headerTexts: new[] { "Parça Adı", "" });
        var result = service.Transfer(project, report, request);

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Equal("Parça Adı", table.Columns[0].Header);
        Assert.Equal("Sütun B", table.Columns[1].Header); // blank header cell falls back to the column letter
        Assert.Empty(project.DataSources[0].ColumnMappings); // no mapping was ever applied
    }

    /// <summary>Transferring with no selection and no heading target inserts a new table at the default Body insertion point.</summary>
    [Fact]
    public void TransferWithNoTarget_CreatesNewTableInBodySection()
    {
        var (project, report) = CreateProjectAndReport();
        var service = new ExcelReportTransferService();

        var result = service.Transfer(project, report, BuildRequest());

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.True(result.CreatedNewTable);
        var bodyChildren = report.Pages[0].Sections[0].Root.Children;
        Assert.Single(bodyChildren);
        Assert.Same(result.Table, bodyChildren[0]);
    }

    /// <summary>Selecting a heading creates the new table immediately under that heading rather than at the default insertion point.</summary>
    [Fact]
    public void TransferWithHeadingSelected_CreatesTableUnderHeading()
    {
        var (project, report) = CreateProjectAndReport();
        var heading = new TextElement { Name = "Heading", Style = HeadingStylePresets.CreateHeadingStyle(), Content = Expression.Literal("Bölüm 1") };
        var trailingParagraph = new TextElement { Content = Expression.Literal("...") };
        var body = report.Pages[0].Sections[0].Root;
        body.Children.Add(heading);
        body.Children.Add(trailingParagraph);

        var service = new ExcelReportTransferService();
        var result = service.Transfer(project, report, BuildRequest(targetElementId: heading.Id));

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.True(result.CreatedNewTable);
        Assert.Equal(3, body.Children.Count);
        Assert.Same(result.Table, body.Children[1]); // inserted directly after the heading, before the trailing paragraph
    }

    /// <summary>Transferring into an already-bound, user-customized table must NOT silently overwrite it — the caller gets a decision request instead.</summary>
    [Fact]
    public void TransferIntoConfiguredTable_WithoutDecision_RequiresExistingTableDecision()
    {
        var (project, report) = CreateProjectAndReport();
        var table = new TableElement { Name = "Tablo 1" };
        table.Columns.Add(new TableColumn { Header = "Parça No", SourceField = "A" });
        table.Binding = new Domain.DataBinding.Binding { DataSourceName = "Existing", WorksheetName = "Sheet1" };
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var service = new ExcelReportTransferService();
        var result = service.Transfer(project, report, BuildRequest(targetElementId: table.Id));

        Assert.Equal(TransferOutcome.RequiresExistingTableDecision, result.Outcome);
        Assert.Equal("Tablo 1", result.Table?.Name);
        // Nothing was mutated while awaiting the decision.
        Assert.Equal("Parça No", table.Columns[0].Header);
        Assert.Equal("Existing", table.Binding.DataSourceName);
    }

    /// <summary>RebindKeepColumns: the displayed columns are preserved; only the source identity/binding is re-pointed.</summary>
    [Fact]
    public void TransferIntoConfiguredTable_RebindKeepColumns_PreservesDisplayedHeaders()
    {
        var (project, report) = CreateProjectAndReport();
        var table = new TableElement { Name = "Tablo 1" };
        table.Columns.Add(new TableColumn { Header = "Parça Adı", SourceField = "OldField" });
        table.Binding = new Domain.DataBinding.Binding { DataSourceName = "Existing", WorksheetName = "OldSheet" };
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var service = new ExcelReportTransferService();
        var request = BuildRequest(targetElementId: table.Id, existingTableMode: ExistingTableTransferMode.RebindKeepColumns);
        var result = service.Transfer(project, report, request);

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Equal("Parça Adı", table.Columns[0].Header); // displayed header untouched
        Assert.Equal("A", table.Columns[0].SourceField); // but source identity re-pointed at the new range
        Assert.Equal("Sheet1", table.Binding.WorksheetName);
    }

    /// <summary>ReplaceColumnsFromSource on a configured table replaces the displayed structure entirely.</summary>
    [Fact]
    public void TransferIntoConfiguredTable_ReplaceColumnsFromSource_ReplacesHeaders()
    {
        var (project, report) = CreateProjectAndReport();
        var table = new TableElement { Name = "Tablo 1" };
        table.Columns.Add(new TableColumn { Header = "Eski Başlık", SourceField = "OldField" });
        table.Binding = new Domain.DataBinding.Binding { DataSourceName = "Existing", WorksheetName = "OldSheet" };
        report.Pages[0].Sections[0].Root.Children.Add(table);

        var service = new ExcelReportTransferService();
        var request = BuildRequest(targetElementId: table.Id, existingTableMode: ExistingTableTransferMode.ReplaceColumnsFromSource);
        var result = service.Transfer(project, report, request);

        Assert.Equal(TransferOutcome.Success, result.Outcome);
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal("Ad", table.Columns[0].Header);
        Assert.Equal("Değer", table.Columns[1].Header);
    }

    /// <summary>Transferring the same workbook path twice reuses the same DataSource (stable naming/no duplicate sources).</summary>
    [Fact]
    public void TransferringSameWorkbookTwice_ReusesSameDataSource()
    {
        var (project, report) = CreateProjectAndReport();
        var service = new ExcelReportTransferService();

        service.Transfer(project, report, BuildRequest());
        service.Transfer(project, report, BuildRequest());

        Assert.Single(project.DataSources);
    }
}
