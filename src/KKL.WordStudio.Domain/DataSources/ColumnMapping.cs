namespace KKL.WordStudio.Domain.DataSources;

/// <summary>
/// Maps a raw spreadsheet column to a logical <see cref="DataBinding.DataField"/>.
/// This is where "Excel column ↔ report field" translation happens — once,
/// at the data-source level — so report elements only ever bind to logical
/// field names via Expression (e.g. "=Fields.CustomerName"), never to raw
/// column letters/indices.
/// </summary>
public sealed class ColumnMapping
{
    /// <summary>Either a column letter ("B") or a stable working-data source field.</summary>
    public required string SourceColumn { get; init; }

    public required Domain.DataBinding.DataField TargetField { get; init; }

    /// <summary>
    /// Whether this source column participates in the next standard Word-table
    /// transfer. Existing projects default to included for backward compatibility.
    /// </summary>
    public bool IsIncluded { get; set; } = true;

    /// <summary>
    /// Optional canonical Excel semantic role name (ItemNumber, PartNumber,
    /// SerialNumber, etc.). Kept as text so Domain does not depend on Application.
    /// </summary>
    public string? SemanticRole { get; set; }
}
