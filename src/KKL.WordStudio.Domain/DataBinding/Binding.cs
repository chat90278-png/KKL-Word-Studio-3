namespace KKL.WordStudio.Domain.DataBinding;

using KKL.WordStudio.Domain.Expressions;

/// <summary>
/// Declares that a report element (TableElement, DataRegion) is bound to a
/// named DataSource within the owning Project, plus any per-element
/// filtering/sorting of that data source's rows.
///
/// WorksheetName was added (Variant 2.5 UI task / ADR 0009) after a real
/// defect was found: ExcelDataSource.ActiveWorksheetName is a single
/// mutable field shared by every Binding pointing at that DataSource, so
/// two tables bound to the same Excel file but different worksheets would
/// both silently follow whichever worksheet the user happened to be
/// browsing. WorksheetName pins a Binding to a specific worksheet,
/// independent of ActiveWorksheetName. Null preserves the old
/// "follow the active worksheet" behavior for any Binding created before
/// this field existed.
///
/// Still deliberately does NOT carry DataRange or ColumnMapping (considered
/// and rejected in Sprint 2): those remain resolved once, at the
/// Worksheet/DataSource level (Worksheet.SelectedRange,
/// DataSource.ColumnMappings). Duplicating them here would create two
/// sources of truth that could disagree. Filter/Sort, by contrast, are
/// legitimately per-element — two tables can read the same DataSource but
/// want different subsets/ordering of its rows.
/// </summary>
public sealed class Binding
{
    public required string DataSourceName { get; set; }

    /// <summary>
    /// Pins this binding to a specific worksheet of the named DataSource
    /// (when the DataSource is Excel-backed and has more than one
    /// worksheet). Null means "follow the DataSource's current active
    /// worksheet" — the pre-Variant-2.5 behavior, kept as the default so
    /// existing bindings created before this field existed keep working
    /// exactly as before.
    /// </summary>
    public string? WorksheetName { get; set; }

    /// <summary>Optional boolean expression (e.g. "=Fields.Region = 'North'") narrowing which rows this element consumes.</summary>
    public Expression? Filter { get; set; }

    public List<SortField> SortFields { get; } = new();
}
