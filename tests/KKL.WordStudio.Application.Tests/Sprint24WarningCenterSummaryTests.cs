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
        Assert.Contains("PN/key '…'", group.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("A-1", group.Message, StringComparison.Ordinal);
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

    [Fact]
    public void Group_DoesNotMergeDifferentProblemTemplatesOnSameTable()
    {
        var elementId = Guid.NewGuid();
        var missingQuantity = Diagnostic("d1", PreviewDiagnosticSeverity.Warning, elementId, "K1");
        var conflictingName = new PreviewDiagnostic
        {
            Id = "d2",
            Severity = PreviewDiagnosticSeverity.Warning,
            Title = "Birleştirilecek satırlarda çelişki var",
            Message = "PN/key 'K2' için 'Tr Isim' alanında çelişkili değerler var; satırlar birleştirilmedi.",
            ElementId = elementId,
            ElementName = "Tablo 1",
            KeyValue = "K2"
        };

        Assert.Collection(
            PreviewDiagnosticSummaryService.Group([missingQuantity, conflictingName]),
            _ => { },
            _ => { });
    }

    private static PreviewDiagnostic Diagnostic(
        string id,
        PreviewDiagnosticSeverity severity,
        Guid elementId,
        string? key) => new()
    {
        Id = id,
        Severity = severity,
        Title = "Adet değeri eksik veya geçersiz",
        Message = string.IsNullOrWhiteSpace(key)
            ? "Geçerli Adet değeri bulunamadı; satırlar birleştirilmedi."
            : $"PN/key '{key}' için geçerli Adet değeri bulunamadı; satırlar birleştirilmedi.",
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
