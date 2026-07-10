namespace KKL.WordStudio.Domain.DataBinding;

using KKL.WordStudio.Domain.DataSources;

/// <summary>
/// One persisted, ordered input of a composed report table. Unlike legacy
/// Binding, the worksheet and configured range are pinned at add time and the
/// field normalization is stored per table source.
/// </summary>
public sealed class TableSourceBinding
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string DataSourceName { get; set; }
    public required string WorksheetName { get; set; }
    public required DataRange Range { get; init; }
    public List<TableSourceFieldMapping> FieldMappings { get; } = new();
}

/// <summary>
/// Maps one stable report-table column identity to the concrete provider field
/// of this source. SourceField is deliberately not the displayed header.
/// </summary>
public sealed class TableSourceFieldMapping
{
    public required Guid TableColumnId { get; init; }
    public required string SourceField { get; set; }
}
