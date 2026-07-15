namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// One non-blocking issue surfaced by Preview. Diagnostics keep their original
/// human-readable message while carrying stable semantic identity and optional
/// report/source navigation metadata. They are runtime projection data and are
/// never persisted.
/// </summary>
public sealed class PreviewDiagnostic
{
    public required string Id { get; init; }

    /// <summary>Stable machine-readable problem identity.</summary>
    public string Code { get; init; } = PreviewDiagnosticCodes.Unclassified;

    /// <summary>
    /// Stable grouping identity produced by the diagnostic factory. Legacy
    /// callers may leave it empty; the summary service retains a compatibility
    /// fallback for those instances.
    /// </summary>
    public string GroupingKey { get; init; } = string.Empty;

    public required PreviewDiagnosticSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public Guid? ElementId { get; init; }
    public string? ElementName { get; init; }
    public string? AffectedColumn { get; init; }
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
