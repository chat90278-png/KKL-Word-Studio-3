namespace KKL.WordStudio.Application.Transfer;

using KKL.WordStudio.Domain.DataSources;
using KKL.WordStudio.Domain.Elements;

/// <summary>
/// Everything the direct "Word'e Aktar" transfer needs, captured from the
/// CURRENT ACTIVE Excel Workspace state — the user is never asked to
/// re-select the workbook/worksheet/range in a second dialog. Column
/// mappings are deliberately OPTIONAL: the normal transfer path works from
/// the configured range + header row alone (see ADR 0011).
/// </summary>
public sealed class ExcelTransferRequest
{
    /// <summary>Full path of the source workbook currently active in the Excel Workspace.</summary>
    public required string WorkbookFilePath { get; init; }

    /// <summary>Display file name of the workbook (e.g. "Aircraft.xlsx").</summary>
    public required string WorkbookFileName { get; init; }

    /// <summary>The worksheet currently active in the Excel Workspace — becomes the new Binding's pinned WorksheetName.</summary>
    public required string WorksheetName { get; init; }

    /// <summary>The configured data range (header row, data start/end, columns) of the active worksheet.</summary>
    public required DataRange Range { get; init; }

    /// <summary>
    /// Raw text of the header row cells (aligned with the range's columns),
    /// used as the DEFAULT displayed report table headers when no column
    /// mapping exists. Empty when the range has no header row.
    /// </summary>
    public IReadOnlyList<string> HeaderTexts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Column mappings the user explicitly applied in the (optional) mapping
    /// drawer. Null/empty means "no mapping" — the transfer then uses the
    /// Excel header text directly. Never required.
    /// </summary>
    public IReadOnlyList<TransferColumnMapping>? AppliedColumnMappings { get; init; }

    /// <summary>
    /// Ordered project-owned working-data columns, when the current worksheet
    /// has been edited. These preserve stable SourceField identity even after
    /// column insert/delete operations. Null means source-range columns.
    /// </summary>
    public IReadOnlyList<TransferWorkingColumn>? WorkingDataColumns { get; init; }

    /// <summary>The report element currently selected in the shared Workspace state (table → transfer into it; heading → create a table under it; null/other → default insertion).</summary>
    public Guid? TargetElementId { get; init; }

    /// <summary>
    /// How to treat a target table that is already configured (bound and/or
    /// with user-customized columns). Null means "not decided yet" — the
    /// service then returns <see cref="TransferOutcome.RequiresExistingTableDecision"/>
    /// instead of silently destroying the user's customization.
    /// </summary>
    public ExistingTableTransferMode? ExistingTableMode { get; init; }

    /// <summary>Explicit per-table-column source-field selections supplied after a mapping-required result.</summary>
    public IReadOnlyList<TransferSourceFieldMapping>? SourceFieldMappings { get; init; }

    /// <summary>Optional user-provided data source name; when empty a stable name is derived from the workbook file name.</summary>
    public string? PreferredDataSourceName { get; init; }
}

/// <summary>One ordered working-data column supplied by the Excel Workspace.</summary>
public sealed class TransferWorkingColumn
{
    public required string SourceField { get; init; }
    public required string Header { get; init; }
    public string? OriginalSourceColumn { get; init; }
}

/// <summary>A single optional mapping row supplied by the Excel Workspace mapping drawer.</summary>
public sealed class TransferColumnMapping
{
    public required string SourceColumn { get; init; }
    public required string FieldName { get; init; }
    public string DataType { get; init; } = "string";
}

/// <summary>The two explicit choices for transferring into an already-configured table — "rebind the data" vs "replace the displayed column structure" are different operations and are never merged silently.</summary>
public enum ExistingTableTransferMode
{
    /// <summary>Keep the table's current displayed columns; only re-point their source identity and the Binding at the active range.</summary>
    RebindKeepColumns,

    /// <summary>Replace the table's columns with the source range's columns (headers from Excel header/mappings), then bind.</summary>
    ReplaceColumnsFromSource,

    /// <summary>Preserve current table structure and append the current configured worksheet as another ordered table source.</summary>
    AddAsSource
}



/// <summary>Explicit field match supplied by the compact source-matching UI.</summary>
public sealed class TransferSourceFieldMapping
{
    public required Guid TableColumnId { get; init; }
    public required string SourceField { get; init; }
}

public sealed class SourceFieldOption
{
    public required string SourceField { get; init; }
    public required string DisplayText { get; init; }
}

public sealed class SourceFieldMappingRequirement
{
    public required Guid TableColumnId { get; init; }
    public required string TableColumnHeader { get; init; }
    public string? SuggestedSourceField { get; init; }
    public required IReadOnlyList<SourceFieldOption> AvailableSourceFields { get; init; }
}

public enum TransferOutcome
{
    Success,

    /// <summary>The target table is already configured; the caller must ask the user to choose an <see cref="ExistingTableTransferMode"/> and retry.</summary>
    RequiresExistingTableDecision,

    /// <summary>An additional source could not be normalized safely without explicit per-column matching.</summary>
    RequiresSourceFieldMapping,

    Failed
}

/// <summary>Result of a direct transfer — carries what the UI needs for status text, selection and refresh.</summary>
public sealed class ExcelTransferResult
{
    public required TransferOutcome Outcome { get; init; }

    /// <summary>The table that received the binding (existing or newly created). Null unless Outcome == Success (or RequiresExistingTableDecision, where it identifies the table awaiting the decision).</summary>
    public TableElement? Table { get; init; }

    public bool CreatedNewTable { get; init; }

    public string? WorksheetName { get; init; }

    /// <summary>Human-readable A1 reference of the transferred range, e.g. "A3:F13".</summary>
    public string? RangeReference { get; init; }

    /// <summary>User-facing (Turkish) failure text. Null unless Outcome == Failed.</summary>
    public string? Error { get; init; }

    public bool AddedAsSource { get; init; }

    public IReadOnlyList<SourceFieldMappingRequirement> SourceFieldMappingRequirements { get; init; } = Array.Empty<SourceFieldMappingRequirement>();

    public static ExcelTransferResult Failure(string error) => new() { Outcome = TransferOutcome.Failed, Error = error };
}
