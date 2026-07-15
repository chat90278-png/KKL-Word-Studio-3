namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Preview;
using Xunit;

public sealed class WordExportPreflightPolicyTests
{
    [Fact]
    public void EmptySnapshot_IsReady()
    {
        var result = WordExportPreflightPolicy.Evaluate([]);

        Assert.Equal(WordExportPreflightStatus.Ready, result.Status);
        Assert.True(result.CanExport);
        Assert.Equal(0, result.GroupCount);
        Assert.Equal(0, result.FindingCount);
    }

    [Fact]
    public void WarningAndInformation_AreVisibleButDoNotBlockExport()
    {
        var groups = new[]
        {
            Group(PreviewDiagnosticSeverity.Warning, occurrenceCount: 4),
            Group(PreviewDiagnosticSeverity.Information, occurrenceCount: 2)
        };

        var result = WordExportPreflightPolicy.Evaluate(groups);

        Assert.Equal(WordExportPreflightStatus.ReadyWithFindings, result.Status);
        Assert.True(result.CanExport);
        Assert.Equal(1, result.WarningGroupCount);
        Assert.Equal(1, result.InformationGroupCount);
        Assert.Equal(2, result.NonBlockingGroupCount);
        Assert.Equal(6, result.NonBlockingFindingCount);
    }

    [Fact]
    public void AnyErrorGroup_BlocksBeforeFileSelection()
    {
        var groups = new[]
        {
            Group(PreviewDiagnosticSeverity.Error, occurrenceCount: 3),
            Group(PreviewDiagnosticSeverity.Warning, occurrenceCount: 5)
        };

        var result = WordExportPreflightPolicy.Evaluate(groups);

        Assert.Equal(WordExportPreflightStatus.Blocked, result.Status);
        Assert.False(result.CanExport);
        Assert.Equal(1, result.ErrorGroupCount);
        Assert.Equal(3, result.ErrorFindingCount);
        Assert.Equal(2, result.GroupCount);
        Assert.Equal(8, result.FindingCount);
    }

    private static PreviewDiagnosticGroup Group(
        PreviewDiagnosticSeverity severity,
        int occurrenceCount)
    {
        var diagnostic = new PreviewDiagnostic
        {
            Id = Guid.NewGuid().ToString("N"),
            Severity = severity,
            Title = "Test finding",
            Message = "Test finding"
        };

        return new PreviewDiagnosticGroup
        {
            Severity = severity,
            Title = diagnostic.Title,
            Message = diagnostic.Message,
            OccurrenceCount = occurrenceCount,
            Representative = diagnostic
        };
    }
}
