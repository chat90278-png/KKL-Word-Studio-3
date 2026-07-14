namespace KKL.WordStudio.Application.Transfer;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
using KKL.WordStudio.Domain.Styling;
using KKL.WordStudio.Domain.Visitors;

public enum ExcelTransferDestinationMode
{
    UpdateExistingTable,
    CreateNewTable
}

public sealed class TransferColumnSelection
{
    public required string ProviderField { get; init; }
    public required string LogicalField { get; init; }
    public required string Header { get; init; }
    public required ExcelSemanticFieldRole SemanticRole { get; init; }
    public required int SourceOrder { get; init; }
    public bool IsIncluded { get; init; }
}

public sealed class ExcelTransferPlacementRequest
{
    public required ExcelTransferRequest Transfer { get; init; }
    public required ExcelTransferDestinationMode DestinationMode { get; init; }
    public Guid? ExistingTableId { get; init; }
    public Guid? AnchorElementId { get; init; }
    public required string TableName { get; init; }
    public bool IncludeHeading { get; init; }
    public string HeadingText { get; init; } = "Yeni başlık";
    public bool IncludeAltHeading { get; init; }
    public string AltHeadingText { get; init; } = "Yeni alt başlık";
    public IReadOnlyList<TransferColumnSelection> Columns { get; init; } = Array.Empty<TransferColumnSelection>();
}

public sealed class ExcelTransferPlacementResult
{
    public required ExcelTransferResult TransferResult { get; init; }
    public IReadOnlyList<Guid> CreatedElementIds { get; init; } = Array.Empty<Guid>();
}

/// <summary>
/// Application-level coordinator for the Word-transfer confirmation surface.
/// It keeps the existing transfer engine authoritative, adds/removes the proposed
/// heading chain atomically, preserves selected Excel columns in source order and
/// creates a normal report text element for the visible table title so Preview and
/// Word consume the same title without a second rendering path.
/// </summary>
public static class ExcelTransferPlacementCoordinator
{
    public const string DefaultRootHeadingText = "System Test Procedure Configuration List";
    public const string TableTitleElementNamePrefix = "Table Title:";

    public static ExcelTransferPlacementResult Transfer(
        IExcelReportTransferService transferService,
        Project project,
        Report report,
        ExcelTransferPlacementRequest placement)
    {
        ArgumentNullException.ThrowIfNull(transferService);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(placement);

        var includedColumns = OrderColumns(placement.Columns.Where(column => column.IsIncluded)).ToList();
        if (includedColumns.Count == 0)
        {
            return new ExcelTransferPlacementResult
            {
                TransferResult = ExcelTransferResult.Failure("Aktarılacak en az bir sütun seçin.")
            };
        }

        return placement.DestinationMode == ExcelTransferDestinationMode.UpdateExistingTable
            ? UpdateExistingTable(transferService, project, report, placement, includedColumns)
            : CreateNewStructure(transferService, project, report, placement, includedColumns);
    }

    /// <summary>
    /// Preserves the physical left-to-right order from the active Excel grid.
    /// Semantic roles still drive automatic selection and binding identity, but
    /// they no longer rearrange the user's chosen source columns.
    /// </summary>
    public static IReadOnlyList<TransferColumnSelection> OrderColumns(IEnumerable<TransferColumnSelection> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        return columns
            .OrderBy(column => column.SourceOrder)
            .ToList();
    }

    private static ExcelTransferPlacementResult UpdateExistingTable(
        IExcelReportTransferService transferService,
        Project project,
        Report report,
        ExcelTransferPlacementRequest placement,
        IReadOnlyList<TransferColumnSelection> includedColumns)
    {
        if (placement.ExistingTableId is not { } tableId
            || ReportElementFlattener.FindById(report, tableId) is not TableElement existingTable)
        {
            return new ExcelTransferPlacementResult
            {
                TransferResult = ExcelTransferResult.Failure("Güncellenecek tablo artık raporda bulunmuyor.")
            };
        }

        var transferRequest = CloneTransfer(
            placement.Transfer,
            targetElementId: tableId,
            existingTableMode: ExistingTableTransferMode.ReplaceColumnsFromSource);
        var result = transferService.Transfer(project, report, transferRequest);
        if (result.Outcome != TransferOutcome.Success || result.Table is null)
            return new ExcelTransferPlacementResult { TransferResult = result };

        ApplyTableIdentityAndColumns(result.Table, placement.TableName, includedColumns);
        EnsureTableTitle(report, result.Table, result.Table.Name);
        return new ExcelTransferPlacementResult { TransferResult = result };
    }

    private static ExcelTransferPlacementResult CreateNewStructure(
        IExcelReportTransferService transferService,
        Project project,
        Report report,
        ExcelTransferPlacementRequest placement,
        IReadOnlyList<TransferColumnSelection> includedColumns)
    {
        var body = EnsureBodySection(report);
        if (body is null)
        {
            return new ExcelTransferPlacementResult
            {
                TransferResult = ExcelTransferResult.Failure("Etkin raporda sayfa bulunamadı.")
            };
        }

        var created = new List<ReportElement>();
        var rootHeading = FindRootHeading(body.Root) ?? CreateRootHeading(body.Root, created);
        var insertionIndex = ResolveInsertionIndex(body.Root, placement.AnchorElementId, rootHeading);

        TextElement? heading = null;
        TextElement? altHeading = null;
        if (placement.IncludeHeading)
        {
            heading = new TextElement
            {
                Name = "Heading",
                Style = HeadingStylePresets.CreateHeadingStyle(),
                Content = Expression.Literal(NormalizeTitle(placement.HeadingText, "Yeni başlık"))
            };
            body.Root.Children.Insert(insertionIndex++, heading);
            created.Add(heading);
        }

        if (placement.IncludeAltHeading)
        {
            altHeading = new TextElement
            {
                Name = "Alt Heading",
                Style = placement.IncludeHeading
                    ? HeadingStylePresets.CreateAltHeadingStyle()
                    : HeadingStylePresets.CreateHeadingStyle(),
                Content = Expression.Literal(NormalizeTitle(placement.AltHeadingText, "Yeni alt başlık"))
            };
            body.Root.Children.Insert(insertionIndex++, altHeading);
            created.Add(altHeading);
        }

        var anchorId = altHeading?.Id ?? heading?.Id ?? rootHeading.Id;
        var transferRequest = CloneTransfer(
            placement.Transfer,
            targetElementId: anchorId,
            existingTableMode: null);
        var result = transferService.Transfer(project, report, transferRequest);

        if (result.Outcome != TransferOutcome.Success || result.Table is null)
        {
            RollBack(body.Root, created);
            return new ExcelTransferPlacementResult { TransferResult = result };
        }

        ApplyTableIdentityAndColumns(result.Table, placement.TableName, includedColumns);
        var tableTitle = EnsureTableTitle(body.Root, result.Table, result.Table.Name);
        if (tableTitle is not null)
            created.Add(tableTitle);

        return new ExcelTransferPlacementResult
        {
            TransferResult = result,
            CreatedElementIds = created.Select(element => element.Id).Append(result.Table.Id).ToList()
        };
    }

    private static void ApplyTableIdentityAndColumns(
        TableElement table,
        string tableName,
        IReadOnlyList<TransferColumnSelection> includedColumns)
    {
        table.Name = NormalizeTitle(tableName, table.Name);
        table.Columns.Clear();
        foreach (var selection in includedColumns)
        {
            table.Columns.Add(new TableColumn
            {
                Header = NormalizeTitle(selection.Header, selection.LogicalField),
                SourceField = selection.LogicalField
            });
        }

        table.SerialQuantityGrouping = new SerialQuantityGroupingDetector().Detect(table.Columns);
        if (!table.Rows.Any(row => row.Kind == TableRowKind.Header))
            table.Rows.Insert(0, new TableRow { Kind = TableRowKind.Header });
        if (!table.Rows.Any(row => row.Kind == TableRowKind.Detail))
            table.Rows.Add(new TableRow { Kind = TableRowKind.Detail });
    }

    private static TextElement? EnsureTableTitle(Report report, TableElement table, string tableName)
    {
        foreach (var section in report.Pages.SelectMany(page => page.Sections))
        {
            var container = FindContainerOf(section.Root, table);
            if (container is not null)
                return EnsureTableTitle(container, table, tableName);
        }
        return null;
    }

    private static TextElement? EnsureTableTitle(Container container, TableElement table, string tableName)
    {
        var tableIndex = container.Children.IndexOf(table);
        if (tableIndex < 0)
            return null;

        var titleElementName = TableTitleElementNamePrefix + table.Id.ToString("N");
        var title = container.Children
            .OfType<TextElement>()
            .FirstOrDefault(element => string.Equals(element.Name, titleElementName, StringComparison.Ordinal));

        if (title is null)
        {
            title = new TextElement
            {
                Name = titleElementName,
                Style = new Style { FontSize = 11, Bold = true },
                Content = Expression.Literal(NormalizeTitle(tableName, table.Name))
            };
            container.Children.Insert(tableIndex, title);
            return title;
        }

        title.Content = Expression.Literal(NormalizeTitle(tableName, table.Name));
        title.Style.Bold = true;
        if (title.Style.FontSize < 10)
            title.Style.FontSize = 11;

        var currentTitleIndex = container.Children.IndexOf(title);
        tableIndex = container.Children.IndexOf(table);
        if (currentTitleIndex != tableIndex - 1)
        {
            container.Children.Remove(title);
            tableIndex = container.Children.IndexOf(table);
            container.Children.Insert(tableIndex, title);
        }

        return title;
    }

    private static ExcelTransferRequest CloneTransfer(
        ExcelTransferRequest source,
        Guid? targetElementId,
        ExistingTableTransferMode? existingTableMode) => new()
    {
        WorkbookFilePath = source.WorkbookFilePath,
        WorkbookFileName = source.WorkbookFileName,
        WorksheetName = source.WorksheetName,
        Range = source.Range,
        HeaderTexts = source.HeaderTexts,
        AppliedColumnMappings = source.AppliedColumnMappings,
        WorkingDataColumns = source.WorkingDataColumns,
        TargetElementId = targetElementId,
        ExistingTableMode = existingTableMode,
        SourceFieldMappings = source.SourceFieldMappings,
        PreferredDataSourceName = source.PreferredDataSourceName
    };

    private static Section? EnsureBodySection(Report report)
    {
        var body = report.Pages.SelectMany(page => page.Sections)
            .FirstOrDefault(section => section.Kind == SectionKind.Body);
        if (body is not null)
            return body;

        var page = report.Pages.FirstOrDefault();
        if (page is null)
            return null;

        body = new Section { Name = SectionKind.Body.ToString(), Kind = SectionKind.Body, AutoHeight = true };
        page.Sections.Add(body);
        return body;
    }

    private static TextElement? FindRootHeading(Container root) =>
        root.Children.OfType<TextElement>().FirstOrDefault(text =>
            HeadingStylePresets.IsHeading(text.Style)
            && string.Equals(text.Name, "Document Root", StringComparison.Ordinal));

    private static TextElement CreateRootHeading(Container root, ICollection<ReportElement> created)
    {
        var rootHeading = new TextElement
        {
            Name = "Document Root",
            Style = HeadingStylePresets.CreateHeadingStyle(),
            Content = Expression.Literal(DefaultRootHeadingText)
        };
        root.Children.Insert(0, rootHeading);
        created.Add(rootHeading);
        return rootHeading;
    }

    private static int ResolveInsertionIndex(Container root, Guid? anchorElementId, TextElement rootHeading)
    {
        if (anchorElementId is { } anchorId)
        {
            var anchorIndex = root.Children.FindIndex(element => element.Id == anchorId);
            if (anchorIndex >= 0)
                return anchorIndex + 1;
        }

        var rootIndex = root.Children.IndexOf(rootHeading);
        return Math.Max(rootIndex + 1, root.Children.Count);
    }

    private static Container? FindContainerOf(Container container, ReportElement element)
    {
        if (container.Children.Contains(element))
            return container;

        foreach (var nested in container.Children.OfType<Container>())
        {
            var found = FindContainerOf(nested, element);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static void RollBack(Container root, IEnumerable<ReportElement> created)
    {
        foreach (var element in created.Reverse())
            root.Children.Remove(element);
    }

    private static string NormalizeTitle(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}