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
        Assert.True(ReportFlowPaginationPolicy.ParticipatesInHeadingChain(CreateText(
            ReportContentKind.Heading,
            "Heading")));
        Assert.True(ReportFlowPaginationPolicy.ParticipatesInHeadingChain(CreateText(
            ReportContentKind.AltHeading,
            "Alt heading")));
    }

    [Fact]
    public void HeadingAltHeadingAndTable_ResolveAsOneStartChain()
    {
        IReadOnlyList<ReportContentNode> nodes =
        [
            CreateText(ReportContentKind.Heading, "Heading"),
            CreateText(ReportContentKind.AltHeading, "Alt heading"),
            CreateTable(),
            CreateText(ReportContentKind.Paragraph, "Body")
        ];

        var endIndex = ReportFlowPaginationPolicy.ResolveKeepWithNextChainEndIndex(nodes, 0);

        Assert.Equal(2, endIndex);
        Assert.True(ReportFlowPaginationPolicy.KeepsWithNext((TextContentNode)nodes[0]));
        Assert.True(ReportFlowPaginationPolicy.KeepsWithNext((TextContentNode)nodes[1]));
    }

    [Fact]
    public void OrdinaryKeepNextParagraph_StopsAfterImmediateNonKeepingBlock()
    {
        var keepingParagraph = new TextContentNode
        {
            ElementId = Guid.NewGuid(),
            Kind = ReportContentKind.Paragraph,
            Text = "Lead",
            Format = new()
            {
                KeepWithNext = true
            }
        };
        IReadOnlyList<ReportContentNode> nodes =
        [
            keepingParagraph,
            CreateText(ReportContentKind.Paragraph, "Following paragraph"),
            CreateTable()
        ];

        var endIndex = ReportFlowPaginationPolicy.ResolveKeepWithNextChainEndIndex(nodes, 0);

        Assert.Equal(1, endIndex);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(25, 3)]
    public void TableStartRequirement_UsesUpToThreeRows(int remainingRows, int expected)
    {
        Assert.Equal(
            expected,
            ReportFlowPaginationPolicy.ResolveMinimumTableStartDataRowCount(remainingRows));
    }

    [Fact]
    public void TableCaptionAndRows_UseCohesivePaginationIntent()
    {
        Assert.True(ReportFlowPaginationPolicy.KeepTableCaptionWithTable("Table 1"));
        Assert.False(ReportFlowPaginationPolicy.KeepTableCaptionWithTable("  "));
        Assert.True(ReportFlowPaginationPolicy.KeepTableRowsIntact);
    }

    private static TextContentNode CreateText(ReportContentKind kind, string text) => new()
    {
        ElementId = Guid.NewGuid(),
        Kind = kind,
        Text = text
    };

    private static TableContentNode CreateTable() => new()
    {
        ElementId = Guid.NewGuid(),
        Kind = ReportContentKind.Table,
        Name = "Table",
        ColumnHeaders = ["No"],
        Rows = [["1"]],
        SourceCount = 0
    };
}
