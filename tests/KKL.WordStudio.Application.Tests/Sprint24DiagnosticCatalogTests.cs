namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Preview;
using Xunit;

public sealed class Sprint24DiagnosticCatalogTests
{
    [Fact]
    public void QuantityWarning_HasStableCodeColumnAndNonBlockingSeverity()
    {
        var rule = PreviewDiagnosticCatalog.ResolveComposition(
            "PN/key '123' için geçerli Adet değeri bulunamadı; satırlar birleştirilmedi.");

        Assert.Equal(PreviewDiagnosticCodes.QuantityInvalid, rule.Code);
        Assert.Equal(PreviewDiagnosticSeverity.Warning, rule.Severity);
        Assert.Equal("Adet", rule.AffectedColumn);
        Assert.Contains("Adet", rule.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSourceFile_IsBlockingError()
    {
        var rule = PreviewDiagnosticCatalog.ResolveSourceError("File not found: source.xlsx");

        Assert.Equal(PreviewDiagnosticCodes.SourceFileMissing, rule.Code);
        Assert.Equal(PreviewDiagnosticSeverity.Error, rule.Severity);
    }

    [Fact]
    public void TableSplit_IsInformationOnly()
    {
        var rule = PreviewDiagnosticCatalog.ResolveLayout("Table split across page fragments.");

        Assert.Equal(PreviewDiagnosticCodes.TableSplit, rule.Code);
        Assert.Equal(PreviewDiagnosticSeverity.Information, rule.Severity);
    }

    [Fact]
    public void StableCode_GroupsDifferentOccurrenceMessagesForSameTableAndColumn()
    {
        var elementId = Guid.NewGuid();
        var diagnostics = new[]
        {
            Diagnostic("q1", elementId, "1001", 4),
            Diagnostic("q2", elementId, "1002", 7)
        };

        var group = Assert.Single(PreviewDiagnosticSummaryService.Group(diagnostics));

        Assert.Equal(PreviewDiagnosticCodes.QuantityInvalid, group.Code);
        Assert.Equal(2, group.OccurrenceCount);
        Assert.Equal(new[] { "1001", "1002" }, group.KeyValues);
        Assert.Equal(new[] { 4, 7 }, group.RowNumbers);
        Assert.Equal("Adet", group.AffectedColumn);
        Assert.Contains("2 kayıt", group.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SameCodeOnDifferentTables_RemainsSeparateAction()
    {
        var diagnostics = new[]
        {
            Diagnostic("q1", Guid.NewGuid(), "1001", 4),
            Diagnostic("q2", Guid.NewGuid(), "1002", 7)
        };

        Assert.Collection(
            PreviewDiagnosticSummaryService.Group(diagnostics),
            _ => { },
            _ => { });
    }

    private static PreviewDiagnostic Diagnostic(string id, Guid elementId, string key, int row) => new()
    {
        Id = id,
        Code = PreviewDiagnosticCodes.QuantityInvalid,
        Severity = PreviewDiagnosticSeverity.Warning,
        Title = "Adet değeri eksik veya geçersiz",
        Message = $"PN/key '{key}' için geçerli Adet değeri bulunamadı; satır {row} birleştirilmedi.",
        ElementId = elementId,
        ElementName = "Tablo 1",
        AffectedColumn = "Adet",
        RowNumber = row,
        KeyValue = key
    };
}
