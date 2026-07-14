namespace KKL.WordStudio.Application.Transfer;

using System.Text.RegularExpressions;
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

public enum ExcelTransferPlacementAnchorKind
{
    Heading,
    AltHeading
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

    /// <summary>
    /// Optional semantic guard used by Quick Report when no new heading is
    /// created. It prevents a table/alt-heading from silently falling back to the
    /// document root or attaching to the wrong outline level.
    /// </summary>
    public ExcelTransferPlacementAnchorKind? RequiredAnchorKind { get; init; }

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

    private static readonly Regex TableNumberPrefix = new(
        @"^\s*Tablo\s+\d+(?:\s*:\s*|\s+|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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

        ApplyTableIdentityAndColumns(report, result.Table, placement.TableName, includedColumns);
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

        ReportElement? requiredAnchor = null;
        if (placement.RequiredAnchorKind is { } requiredKind)
        {
            requiredAnchor = placement.AnchorElementId is { } anchorId
                ? ReportElementFlattener.FindById(report, anchorId)
                : null;
            if (!MatchesRequiredAnchor(requiredAnchor, requiredKind))
            {
                RollBack(body.Root, created);
                var expected = requiredKind == ExcelTransferPlacementAnchorKind.Heading
                    ? "üst başlık"
                    : "alt başlık";
                return new ExcelTransferPlacementResult
                {
                    TransferResult = ExcelTransferResult.Failure($"Seçilen {expected} artık raporda bulunmuyor veya seviyesi değişti.")
                };
            }
        }

        var insertionContainer = requiredAnchor is null
            ? body.Root
            : FindContainerOf(body.Root, requiredAnchor) ?? body.Root;
        var insertionIndex = ResolveInsertionIndex(
            insertionContainer,
            placement.AnchorElementId,
            ReferenceEquals(insertionContainer, body.Root) ? rootHeading : null,
            placement.RequiredAnchorKind);

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
            insertionContainer.Children.Insert(insertionIndex++, heading);
            created.Add(heading);
        }

        if (placement.IncludeAltHeading)
        {
            var hasHeadingParent = placement.IncludeHeading
                || placement.RequiredAnchorKind == ExcelTransferPlacementAnchorKind.Heading;
            altHeading = new TextElement
            {
                Name = hasHeadingParent ? "Alt Heading" : "Heading",
                Style = hasHeadingParent
                    ? HeadingStylePresets.CreateAltHeadingStyle()
                    : HeadingStylePresets.CreateHeadingStyle(),
                Content = Expression.Literal(ReportHeadingNumberingService.StripVisibleNumber(
                    NormalizeTitle(placement.AltHeadingText, "Yeni alt başlık")))
            };
            insertionContainer.Children.Insert(insertionIndex++, altHeading);
            created.Add(altHeading);
        }

        // With both proposal rows disabled, the selected alt heading itself stays
        // the transfer target. Passing a table from its block would update that
        // table instead of creating a new one.
        var anchorIdForTransfer = altHeading?.Id
            ?? heading?.Id
            ?? placement.AnchorElementId
            ?? rootHeading.Id;
        var transferRequest = CloneTransfer(
            placement.Transfer,
            targetElementId: anchorIdForTransfer,
            existingTableMode: null);
        var result = transferService.Transfer(project, report, transferRequest);

        if (result.Outcome != TransferOutcome.Success || result.Table is null)
        {
            RollBack(insertionContainer, created);
            return new ExcelTransferPlacementResult { TransferResult = result };
        }

        if (!placement.IncludeHeading
            && !placement.IncludeAltHeading
            && requiredAnchor is not null
            && placement.RequiredAnchorKind == ExcelTransferPlacementAnchorKind.AltHeading)
        {
            MoveTableToAnchorBlockEnd(insertionContainer, requiredAnchor, result.Table);
        }

        ApplyTableIdentityAndColumns(report, result.Table, placement.TableName, includedColumns);
        RemoveLegacyTableTitle(report, result.Table);
        ReportHeadingNumberingService.Renumber(report);

        return new ExcelTransferPlacementResult
        {
            TransferResult = result,
            CreatedElementIds = created.Select(element => element.Id).Append(result.Table.Id).ToList()
        };
    }

    private static bool MatchesRequiredAnchor(
        ReportElement? element,
        ExcelTransferPlacementAnchorKind requiredKind) =>
        element is TextElement text
        && (requiredKind switch
        {
            ExcelTransferPlacementAnchorKind.Heading =>
                HeadingStylePresets.IsHeading(text.Style)
                && !string.Equals(text.Name, "Document Root", StringComparison.Ordinal),
            ExcelTransferPlacementAnchorKind.AltHeading => HeadingStylePresets.IsAltHeading(text.Style),
            _ => false
        });

    private static void ApplyTableIdentityAndColumns(
        Report report,
        TableElement table,
        string tableName,
        IReadOnlyList<TransferColumnSelection> includedColumns)
    {
        var tableNumber = ResolveTableNumber(report, table);
        table.Name = ResolveRawCaption(tableName, tableNumber);
        table.Caption = ResolveRawCaption(table.Name, tableNumber);
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

    /// <summary>
    /// TableElement.Caption stores only the raw text after the renderer-owned
    /// "Tablo N:" prefix. Generated/default names deliberately remain as
    /// "Tablo N", producing "Tablo N: Tablo N". Any user-entered numeric
    /// prefixes are stripped repeatedly so Preview and Word never duplicate them.
    /// </summary>
    private static string ResolveRawCaption(string? tableName, int tableNumber)
    {
        var fallback = $"Tablo {Math.Max(1, tableNumber)}";
        var normalized = tableName?.Trim() ?? string.Empty;

        while (normalized.Length > 0)
        {
            var match = TableNumberPrefix.Match(normalized);
            if (!match.Success || match.Length == 0)
                break;

            normalized = normalized[match.Length..].Trim();
        }

        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static int ResolveTableNumber(Report report, TableElement table)
    {
        var tables = ReportElementFlattener.Flatten(report).OfType<TableElement>().ToList();
        var index = tables.FindIndex(candidate => candidate.Id == table.Id);
        return index >= 0 ? index + 1 : Math.Max(1, tables.Count);
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

    private static int ResolveInsertionIndex(
        Container container,
        Guid? anchorElementId,
        TextElement? rootHeading,
        ExcelTransferPlacementAnchorKind? requiredKind)
    {
        if (anchorElementId is { } anchorId)
        {
            var anchorIndex = container.Children.FindIndex(element => element.Id == anchorId);
            if (anchorIndex >= 0)
            {
                if (requiredKind is { } kind)
                    return FindAnchorBlockEnd(container, anchorIndex, kind);
                return anchorIndex + 1;
            }
        }

        if (rootHeading is not null)
        {
            var rootIndex = container.Children.IndexOf(rootHeading);
            return Math.Max(rootIndex + 1, container.Children.Count);
        }

        return container.Children.Count;
    }

    private static void MoveTableToAnchorBlockEnd(
        Container container,
        ReportElement anchor,
        TableElement table)
    {
        var anchorIndex = container.Children.IndexOf(anchor);
        if (anchorIndex < 0 || !container.Children.Remove(table))
            return;

        var insertionIndex = FindAnchorBlockEnd(
            container,
            anchorIndex,
            ExcelTransferPlacementAnchorKind.AltHeading);
        container.Children.Insert(insertionIndex, table);
    }

    private static int FindAnchorBlockEnd(
        Container container,
        int anchorIndex,
        ExcelTransferPlacementAnchorKind kind)
    {
        for (var index = anchorIndex + 1; index < container.Children.Count; index++)
        {
            if (container.Children[index] is not TextElement text)
                continue;

            var startsHeading = HeadingStylePresets.IsHeading(text.Style)
                && !string.Equals(text.Name, "Document Root", StringComparison.Ordinal);
            var startsAltHeading = HeadingStylePresets.IsAltHeading(text.Style);
            if (kind == ExcelTransferPlacementAnchorKind.Heading && startsHeading)
                return index;
            if (kind == ExcelTransferPlacementAnchorKind.AltHeading && (startsHeading || startsAltHeading))
                return index;
        }

        return container.Children.Count;
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
