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
        Assert.True(ReportFlowPaginationPolicy.IsHeading(headingKind));
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
        Assert.False(ReportFlowPaginationPolicy.IsHeading(nextKind));
    }

    [Fact]
    public void HeadingWithoutPreviousTable_DoesNotStartNewPage()
    {
        Assert.False(ReportFlowPaginationPolicy.StartsNewPageAfterTable(
            ReportContentKind.Heading,
            ReportContentKind.AltHeading));
    }

    [Fact]
    public void HeadingNodes_ParticipateInOneKeepChain()
    {
        Assert.True(ReportFlowPaginationPolicy.ParticipatesInHeadingChain(new TextContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.Heading,
            Text = "Heading"
        }));
        Assert.True(ReportFlowPaginationPolicy.ParticipatesInHeadingChain(new TextContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.AltHeading,
            Text = "Alt heading"
        }));
    }

    [Fact]
    public void TableCaptionAndRows_UseCohesivePaginationIntent()
    {
        Assert.True(ReportFlowPaginationPolicy.KeepTableCaptionWithTable("Table 1"));
        Assert.False(ReportFlowPaginationPolicy.KeepTableCaptionWithTable("  "));
        Assert.True(ReportFlowPaginationPolicy.KeepTableRowsIntact);
    }
}
