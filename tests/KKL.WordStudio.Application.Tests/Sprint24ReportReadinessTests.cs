namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Preview;
using Xunit;

public sealed class Sprint24ReportReadinessTests
{
    [Fact]
    public void Consolidate_RepeatedRowFindingsBecomeOneNavigableGroup()
    {
        var elementId = Guid.NewGuid();
        var diagnostics = new[]
        {
            Diagnostic("row-1", PreviewDiagnosticSeverity.Warning, elementId, "1001", "A3:F3", "PN/key '1001' yinelenen seri içeriyor."),
            Diagnostic("row-2", PreviewDiagnosticSeverity.Warning, elementId, "1002", "A4:F4", "PN/key '1002' yinelenen seri içeriyor."),
            Diagnostic("row-3", PreviewDiagnosticSeverity.Warning, elementId, "1003", "A5:F5", "PN/key '1003' yinelenen seri içeriyor.")
        };

        var consolidated = PreviewDiagnosticConsolidator.Consolidate(diagnostics);

        var group = Assert.Single(consolidated);
        Assert.Equal(3, group.OccurrenceCount);
        Assert.Null(group.KeyValue);
        Assert.Equal(3, group.Sources.Count);
        Assert.Equal(elementId, group.ElementId);
    }

    [Fact]
    public void Consolidate_DifferentSeverityOrElementRemainSeparateAndErrorsSortFirst()
    {
        var firstElement = Guid.NewGuid();
        var secondElement = Guid.NewGuid();
        var diagnostics = new[]
        {
            Diagnostic("info", PreviewDiagnosticSeverity.Information, firstElement, null, "A1"),
            Diagnostic("warning", PreviewDiagnosticSeverity.Warning, secondElement, null, "A2"),
            Diagnostic("error", PreviewDiagnosticSeverity.Error, firstElement, null, "A3")
        };

        var consolidated = PreviewDiagnosticConsolidator.Consolidate(diagnostics);

        Assert.Equal(3, consolidated.Count);
        Assert.Equal(PreviewDiagnosticSeverity.Error, consolidated[0].Severity);
        Assert.Equal(PreviewDiagnosticSeverity.Warning, consolidated[1].Severity);
        Assert.Equal(PreviewDiagnosticSeverity.Information, consolidated[2].Severity);
    }

    [Fact]
    public void Readiness_ErrorGroupsBlockExport()
    {
        var diagnostics = new[]
        {
            Diagnostic("error-1", PreviewDiagnosticSeverity.Error, Guid.NewGuid(), null, "A1"),
            Diagnostic("error-2", PreviewDiagnosticSeverity.Error, Guid.NewGuid(), null, "A2"),
            Diagnostic("warning", PreviewDiagnosticSeverity.Warning, Guid.NewGuid(), null, "A3")
        };

        var readiness = ReportReadinessAssessment.From(diagnostics);

        Assert.True(readiness.BlocksExport);
        Assert.False(readiness.RequiresWarningConfirmation);
        Assert.Equal(2, readiness.ErrorGroupCount);
        Assert.Equal(2, readiness.ErrorOccurrenceCount);
        Assert.Equal(1, readiness.WarningGroupCount);
    }

    [Fact]
    public void Readiness_WarningsRequireConfirmationButDoNotBlock()
    {
        var elementId = Guid.NewGuid();
        var diagnostics = new[]
        {
            Diagnostic("warning-1", PreviewDiagnosticSeverity.Warning, elementId, "1", "A1"),
            Diagnostic("warning-2", PreviewDiagnosticSeverity.Warning, elementId, "2", "A2"),
            Diagnostic("info", PreviewDiagnosticSeverity.Information, Guid.NewGuid(), null, "A3")
        };

        var readiness = ReportReadinessAssessment.From(diagnostics);

        Assert.False(readiness.BlocksExport);
        Assert.True(readiness.RequiresWarningConfirmation);
        Assert.Equal(1, readiness.WarningGroupCount);
        Assert.Equal(2, readiness.WarningOccurrenceCount);
        Assert.Equal(1, readiness.InformationGroupCount);
        Assert.Equal(3, readiness.TotalOccurrenceCount);
    }

    private static PreviewDiagnostic Diagnostic(
        string id,
        PreviewDiagnosticSeverity severity,
        Guid elementId,
        string? keyValue,
        string range,
        string message = "Aynı seri numarası birden fazla satırda bulundu.") => new()
    {
        Id = id,
        Severity = severity,
        Title = "Tekrarlanan seri numarası",
        Message = message,
        ElementId = elementId,
        ElementName = "Tablo 1",
        KeyValue = keyValue,
        Sources =
        [
            new PreviewDiagnosticSource
            {
                DataSourceName = "source",
                SourcePath = "/tmp/source.xlsx",
                WorksheetName = "Sheet1",
                RangeReference = range,
                KeyColumnIdentity = "SerialNumber"
            }
        ]
    };
}
