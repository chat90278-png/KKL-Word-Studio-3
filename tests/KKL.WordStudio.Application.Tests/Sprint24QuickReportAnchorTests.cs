namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.QuickAssembly;
using Xunit;

public sealed class Sprint24QuickReportAnchorTests
{
    [Fact]
    public void Target_RequiresHeadingWhenOnlyAltHeadingIsCreated()
    {
        var target = Target();
        target.IncludeHeading = false;
        target.IncludeAltHeading = true;

        Assert.True(target.RequiresPlacementAnchor);
        Assert.Equal(QuickAssemblyAnchorKind.Heading, target.RequiredPlacementAnchorKind);
    }

    [Fact]
    public void Target_RequiresAltHeadingWhenOnlyTableIsCreated()
    {
        var target = Target();
        target.IncludeHeading = false;
        target.IncludeAltHeading = false;

        Assert.True(target.RequiresPlacementAnchor);
        Assert.Equal(QuickAssemblyAnchorKind.AltHeading, target.RequiredPlacementAnchorKind);
    }

    [Fact]
    public void Synchronize_PreservesExistingAndEarlierQuickTargetReferences()
    {
        var selection = new QuickAssemblySelection();
        selection.Synchronize([Source("A", "B")]);
        var a = Assert.Single(selection.Targets, target => target.WorksheetName == "A");
        var b = Assert.Single(selection.Targets, target => target.WorksheetName == "B");
        var existingId = Guid.NewGuid();

        a.ExistingPlacementAnchorId = existingId;
        a.PlacementAnchorKind = QuickAssemblyAnchorKind.Heading;
        b.SourcePlacementTargetKey = a.Key;
        b.PlacementAnchorKind = QuickAssemblyAnchorKind.AltHeading;

        selection.Synchronize([Source("A", "B")]);

        a = Assert.Single(selection.Targets, target => target.WorksheetName == "A");
        b = Assert.Single(selection.Targets, target => target.WorksheetName == "B");
        Assert.Equal(existingId, a.ExistingPlacementAnchorId);
        Assert.Equal(QuickAssemblyAnchorKind.Heading, a.PlacementAnchorKind);
        Assert.Equal(a.Key, b.SourcePlacementTargetKey);
        Assert.Equal(QuickAssemblyAnchorKind.AltHeading, b.PlacementAnchorKind);
    }

    [Fact]
    public void Synchronize_RemovesReferenceToStaleQuickTarget()
    {
        var selection = new QuickAssemblySelection();
        selection.Synchronize([Source("A", "B")]);
        var a = Assert.Single(selection.Targets, target => target.WorksheetName == "A");
        var b = Assert.Single(selection.Targets, target => target.WorksheetName == "B");
        b.SourcePlacementTargetKey = a.Key;
        b.PlacementAnchorKind = QuickAssemblyAnchorKind.Heading;

        selection.Synchronize([Source("B")]);

        b = Assert.Single(selection.Targets);
        Assert.Null(b.SourcePlacementTargetKey);
        Assert.Null(b.PlacementAnchorKind);
    }

    [Fact]
    public async Task Batch_PropagatesCreatedHeadingIdentities()
    {
        var headingId = Guid.NewGuid();
        var altHeadingId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var target = Target();
        target.IsSelected = true;
        target.SelectionOrder = 1;

        var result = await new QuickAssemblyBatchOrchestrator().ExecuteAsync(
            [target],
            (_, _) => Task.FromResult(new QuickAssemblyTransferOutcome
            {
                Status = QuickAssemblyTransferStatus.Created,
                CreatedElementId = tableId,
                CreatedHeadingElementId = headingId,
                CreatedAltHeadingElementId = altHeadingId
            }));

        var created = Assert.Single(result.Targets);
        Assert.Equal(tableId, created.CreatedElementId);
        Assert.Equal(headingId, created.CreatedHeadingElementId);
        Assert.Equal(altHeadingId, created.CreatedAltHeadingElementId);
    }

    private static QuickAssemblySourceSnapshot Source(params string[] names) => new()
    {
        SourcePath = "C:/data/source.xlsx",
        DisplayName = "source.xlsx",
        WorksheetNames = names
    };

    private static QuickAssemblyTarget Target() => new()
    {
        SourcePath = "C:/data/source.xlsx",
        WorkbookDisplayName = "source.xlsx",
        WorksheetName = "Sheet1",
        WorkbookOrder = 0,
        WorksheetOrder = 0
    };
}
