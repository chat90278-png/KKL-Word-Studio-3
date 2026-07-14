namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Preview;
using Xunit;

public sealed class Sprint24WarningCenterSummaryTests
{
    [Fact]
    public void Group_RepeatedOccurrencesBecomeOneActionableItem()
    {
        var elementId = Guid.NewGuid();
        var diagnostics = new[]
        {
            Diagnostic("d1", PreviewDiagnosticSeverity.Warning, elementId, "A-1"),
            Diagnostic("d2", PreviewDiagnosticSeverity.Warning, elementId, "A-2"),
            Diagnostic("d3", PreviewDiagnosticSeverity.Warning, elementId, "A-3")
        };

        var group = Assert.Single(PreviewDiagnosticSummaryService.Group(diagnostics));

        Assert.Equal(3, group.OccurrenceCount);
        Assert.Equal(new[] { "A-1", "A-2", "A-3" }, group.KeyValues);
        Assert.Single(group.Sources);
    }

    [Fact]
    public void Group_DifferentReportElementsRemainSeparateActions()
    {
        var diagnostics = new[]
        {
            Diagnostic("d1", PreviewDiagnosticSeverity.Warning, Guid.NewGuid(), "A-1"),
            Diagnostic("d2", PreviewDiagnosticSeverity.Warning, Guid.NewGuid(), "A-1")
        };

        Assert.Collection(
            PreviewDiagnosticSummaryService.Group(diagnostics),
            _ => { },
            _ => { });
    }

    [Fact]
    public void Group_OrdersErrorsBeforeWarningsAndInformation()
    {
        var diagnostics = new[]
        {
            Diagnostic("i", PreviewDiagnosticSeverity.Information, Guid.NewGuid(), null),
            Diagnostic("w", PreviewDiagnosticSeverity.Warning, Guid.NewGuid(), null),
            Diagnostic("e", PreviewDiagnosticSeverity.Error, Guid.NewGuid(), null)
        };

        var groups = PreviewDiagnosticSummaryService.Group(diagnostics);

        Assert.Equal(
            new[]
            {
                PreviewDiagnosticSeverity.Error,
                PreviewDiagnosticSeverity.Warning,
                PreviewDiagnosticSeverity.Information
            },
            groups.Select(group => group.Severity));
    }

    [Fact]
    public void Group_DeduplicatesEquivalentSourcesWithoutLosingNavigation()
    {
        var elementId = Guid.NewGuid();
        var diagnostics = new[]
        {
            Diagnostic("d1", PreviewDiagnosticSeverity.Error, elementId, "K1"),
            Diagnostic("d2", PreviewDiagnosticSeverity.Error, elementId, "K2")
        };

        var group = Assert.Single(PreviewDiagnosticSummaryService.Group(diagnostics));

        Assert.Single(group.Sources);
        Assert.Equal("Sheet1", group.Sources[0].WorksheetName);
        Assert.Collection(group.KeyValues, key => Assert.Equal("K1", key), key => Assert.Equal("K2", key));
        Assert.Equal(elementId, group.ElementId);
    }

    private static PreviewDiagnostic Diagnostic(
        string id,
        PreviewDiagnosticSeverity severity,
        Guid elementId,
        string? key) => new()
    {
        Id = id,
        Severity = severity,
        Title = "Eşleşmeyen kayıt",
        Message = "Kaynak anahtar rapor tablosunda çözülemedi.",
        ElementId = elementId,
        ElementName = "Tablo 1",
        KeyValue = key,
        Sources =
        [
            new PreviewDiagnosticSource
            {
                DataSourceName = "source.xlsx",
                SourcePath = "C:/data/source.xlsx",
                WorksheetName = "Sheet1",
                RangeReference = "A3:F100",
                KeyColumnIdentity = "ItemNumber"
            }
        ]
    };
}
