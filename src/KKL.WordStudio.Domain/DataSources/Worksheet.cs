namespace KKL.WordStudio.Domain.DataSources;

public sealed class Worksheet
{
    public required string Name { get; init; }
    public DataRange? SelectedRange { get; set; }

    /// <summary>
    /// Column mappings belong to the configured worksheet/dataset, not the
    /// workbook-wide ExcelDataSource. The legacy DataSource.ColumnMappings
    /// collection remains for backward compatibility; readers fall back to it
    /// only when this per-worksheet list is empty.
    /// </summary>
    public List<ColumnMapping> ColumnMappings { get; } = new();

    /// <summary>
    /// Optional project-owned editable snapshot for this configured worksheet.
    /// Null means the original Excel source remains authoritative. The snapshot
    /// is created lazily on the first data edit/row-column mutation/paste.
    /// </summary>
    public WorksheetWorkingData? WorkingData { get; set; }
}
