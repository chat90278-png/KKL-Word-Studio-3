namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// One issue surfaced by Preview. Diagnostics retain their original technical
/// message while carrying stable code, severity and navigation metadata. They
/// are runtime projection data and are never persisted.
/// </summary>
public sealed class PreviewDiagnostic
{
    public required string Id { get; init; }
    public string Code { get; init; } = PreviewDiagnosticCodes.Unclassified;
    public required PreviewDiagnosticSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public Guid? ElementId { get; init; }
    public string? ElementName { get; init; }
    public string? AffectedColumn { get; init; }
    public int? RowNumber { get; init; }
    public string? KeyValue { get; init; }
    public IReadOnlyList<PreviewDiagnosticSource> Sources { get; init; } = Array.Empty<PreviewDiagnosticSource>();
}

public enum PreviewDiagnosticSeverity
{
    Information,
    Warning,
    Error
}

/// <summary>One Excel source candidate that can be activated for a diagnostic.</summary>
public sealed class PreviewDiagnosticSource
{
    public required string DataSourceName { get; init; }
    public string? SourcePath { get; init; }
    public string? WorksheetName { get; init; }
    public string? RangeReference { get; init; }
    public string? KeyColumnIdentity { get; init; }
}
