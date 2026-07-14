namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Preview;
using Xunit;

public sealed class Sprint24WordExportReadinessTests
{
    [Fact]
    public void Assessment_CountsGroupsAndOccurrencesBySeverity()
    {
        var assessment = ReportReadinessAssessment.FromGroups(
        [
            Group(PreviewDiagnosticSeverity.Error, 3),
            Group(PreviewDiagnosticSeverity.Warning, 8),
            Group(PreviewDiagnosticSeverity.Warning, 2),
            Group(PreviewDiagnosticSeverity.Information, 5)
        ]);

        Assert.Equal(1, assessment.ErrorGroupCount);
        Assert.Equal(2, assessment.WarningGroupCount);
        Assert.Equal(1, assessment.InformationGroupCount);
        Assert.Equal(3, assessment.ErrorOccurrenceCount);
        Assert.Equal(10, assessment.WarningOccurrenceCount);
        Assert.Equal(5, assessment.InformationOccurrenceCount);
        Assert.Equal(4, assessment.TotalGroupCount);
        Assert.Equal(18, assessment.TotalOccurrenceCount);
    }

    [Fact]
    public void Assessment_ErrorBlocksExportWithoutWarningConfirmation()
    {
        var assessment = ReportReadinessAssessment.FromGroups(
        [
            Group(PreviewDiagnosticSeverity.Error, 1),
            Group(PreviewDiagnosticSeverity.Warning, 4)
        ]);

        Assert.True(assessment.BlocksExport);
        Assert.False(assessment.RequiresWarningConfirmation);
    }

    [Fact]
    public void Assessment_WarningsRequireConfirmationWhileInformationDoesNot()
    {
        var warningAssessment = ReportReadinessAssessment.FromGroups(
        [
            Group(PreviewDiagnosticSeverity.Warning, 7),
            Group(PreviewDiagnosticSeverity.Information, 2)
        ]);
        var informationAssessment = ReportReadinessAssessment.FromGroups(
        [
            Group(PreviewDiagnosticSeverity.Information, 9)
        ]);

        Assert.False(warningAssessment.BlocksExport);
        Assert.True(warningAssessment.RequiresWarningConfirmation);
        Assert.False(informationAssessment.BlocksExport);
        Assert.False(informationAssessment.RequiresWarningConfirmation);
    }

    private static PreviewDiagnosticGroup Group(
        PreviewDiagnosticSeverity severity,
        int occurrenceCount) => new()
    {
        Severity = severity,
        Title = "Finding",
        Message = "Message",
        OccurrenceCount = occurrenceCount,
        Representative = new PreviewDiagnostic
        {
            Id = Guid.NewGuid().ToString("N"),
            Severity = severity,
            Title = "Finding",
            Message = "Message"
        }
    };
}
