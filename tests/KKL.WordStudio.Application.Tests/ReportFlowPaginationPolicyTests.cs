namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Content;
using Xunit;

public sealed class ReportFlowPaginationPolicyTests
{
    [Theory]
    [InlineData(ReportContentKind.Heading)]
    [InlineData(ReportContentKind.AltHeading)]
    public void TableFollowedByHeading_StartsNewPage(ReportContentKind headingKind)
    {
        Assert.True(ReportFlowPaginationPolicy.StartsNewPageAfterTable(
            ReportContentKind.Table,
            headingKind));
    }

    [Theory]
    [InlineData(ReportContentKind.Paragraph)]
    [InlineData(ReportContentKind.Table)]
    [InlineData(ReportContentKind.Image)]
    public void TableFollowedByNonHeading_DoesNotStartNewPage(ReportContentKind nextKind)
    {
        Assert.False(ReportFlowPaginationPolicy.StartsNewPageAfterTable(
            ReportContentKind.Table,
            nextKind));
    }

    [Fact]
    public void HeadingWithoutPreviousTable_DoesNotStartNewPage()
    {
        Assert.False(ReportFlowPaginationPolicy.StartsNewPageAfterTable(
            ReportContentKind.Heading,
            ReportContentKind.AltHeading));
    }
}
