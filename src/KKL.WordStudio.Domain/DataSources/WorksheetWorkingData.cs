namespace KKL.WordStudio.Domain.DataSources;

/// <summary>
/// Project-owned, serializable worksheet data created lazily on the first
/// edit/mutation. It is authoritative for the configured worksheet dataset
/// while present; the original workbook remains a read-only fallback.
/// </summary>
public sealed class WorksheetWorkingData
{
    public List<WorkingDataColumn> Columns { get; } = new();
    public List<WorkingDataRow> Rows { get; } = new();
}

/// <summary>
/// A working-data column with a stable binding identity independent of its
/// current ordinal position. OriginalSourceColumn is retained only for
/// Excel mapping/raw-letter aliases; inserted project-only columns leave it null.
/// </summary>
public sealed class WorkingDataColumn
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string SourceField { get; init; }
    public string Header { get; set; } = string.Empty;
    public string? OriginalSourceColumn { get; init; }
}

/// <summary>Ordered cell values for one working-data row.</summary>
public sealed class WorkingDataRow
{
    /// <summary>
    /// Original 1-based Excel row number when the row came from the source;
    /// null for project-only inserted rows.
    /// </summary>
    public int? OriginalRowNumber { get; init; }

    public List<string?> Values { get; } = new();
}
