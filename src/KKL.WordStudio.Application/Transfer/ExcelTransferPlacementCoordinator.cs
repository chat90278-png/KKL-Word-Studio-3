namespace KKL.WordStudio.Application.Transfer;

using KKL.WordStudio.Application.Excel;
using KKL.WordStudio.Application.Structure;
using KKL.WordStudio.Application.Styling;
using KKL.WordStudio.Application.TableComposition;
using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Domain.Expressions;
using KKL.WordStudio.Domain.Projects;
using KKL.WordStudio.Domain.Reports;
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
/// It keeps the existing transfer engine authoritative, composes the proposed
/// heading chain atomically, preserves the active Excel grid order and delegates
/// visible table numbering to the existing TableElement.Caption pipeline shared
/// by Preview and Word.
/// </summary>
public static class ExcelTransferPlacementCoordinator
{
    public const string DefaultRootHeadingText = "System Test Procedure Configuration List";

    // Kept only to remove titles created by earlier Tranche 02 heads. New output
    // uses TableElement.Caption and the existing SEQ Tablo renderer/exporter.
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
    /// SourceOrder is synchronized from the DataGrid's live DisplayIndex order,
    /// so Preview and Word follow exactly what the user sees from left to right.
    /// </summary>
    public static IReadOnlyList<TransferColumnSelection> OrderColumns(IEnumerable<TransferColumnSelection> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        return columns.OrderBy(column => column.SourceOrder).ToList();
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
        RemoveLegacyTableTitle(report, result.Table);
        ReportHeadingNumberingService.Renumber(report);
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
                Content = Expression.Literal(ReportHeadingNumberingService.StripVisibleNumber(
                    NormalizeTitle(placement.HeadingText, "Yeni başlık")))
            };
            body.Root.Children.Insert(insertionIndex++, heading);
            created.Add(heading);
        }

        if (placement.IncludeAltHeading)
        {
            altHeading = new TextElement
            {
                Name = placement.IncludeHeading ? "Alt Heading" : "Heading",
                Style = placement.IncludeHeading
                    ? HeadingStylePresets.CreateAltHeadingStyle()
                    : HeadingStylePresets.CreateHeadingStyle(),
                Content = Expression.Literal(ReportHeadingNumberingService.StripVisibleNumber(
                    NormalizeTitle(placement.AltHeadingText, "Yeni alt başlık")))
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
        RemoveLegacyTableTitle(report, result.Table);
        ReportHeadingNumberingService.Renumber(report);

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
        var normalizedName = NormalizeTitle(tableName, table.Name);
        table.Name = normalizedName;
        table.Caption = ResolveRawCaption(normalizedName);
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

    private static string? ResolveRawCaption(string tableName)
    {
        if (!tableName.StartsWith("Tablo ", StringComparison.OrdinalIgnoreCase))
            return tableName;

        var separator = tableName.IndexOf(':');
        if (separator >= 0 && separator + 1 < tableName.Length)
            return tableName[(separator + 1)..].Trim();

        return null;
    }

    private static void RemoveLegacyTableTitle(Report report, TableElement table)
    {
        foreach (var section in report.Pages.SelectMany(page => page.Sections))
        {
            var container = FindContainerOf(section.Root, table);
            if (container is null)
                continue;

            var legacyName = TableTitleElementNamePrefix + table.Id.ToString("N");
            var legacyTitle = container.Children.OfType<TextElement>()
                .FirstOrDefault(element => string.Equals(element.Name, legacyName, StringComparison.Ordinal));
            if (legacyTitle is not null)
                container.Children.Remove(legacyTitle);
            return;
        }
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
