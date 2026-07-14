namespace KKL.WordStudio.Domain.DataSources;

/// <summary>
/// Maps a raw spreadsheet column to a logical <see cref="DataBinding.DataField"/>.
/// Logical identity and user-visible header are deliberately separate so a
/// header rename never breaks semantic ordering or data binding.
/// </summary>
public sealed class ColumnMapping
{
    /// <summary>Either a column letter ("B") or a stable working-data source field.</summary>
    public required string SourceColumn { get; init; }

    public required Domain.DataBinding.DataField TargetField { get; init; }

    /// <summary>User-visible/edited header text. Null means use TargetField.Name for legacy projects.</summary>
    public string? DisplayHeader { get; set; }

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
