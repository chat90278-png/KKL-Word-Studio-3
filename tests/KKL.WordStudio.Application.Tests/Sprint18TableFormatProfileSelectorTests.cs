namespace KKL.WordStudio.Application.Tests;

using KKL.WordStudio.Application.Formatting;
using KKL.WordStudio.Domain.Elements;
using Xunit;

public sealed class Sprint18TableFormatProfileSelectorTests
{
    [Fact]
    public void AutomaticSelection_UsesFirstCompatibleProfileWithoutPersistingResolvedKey()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var table = CreateTable(columnCount: 6);

        var selected = Assert.IsType<ReferenceTableFormatProfile>(
            TableFormatProfileSelector.SelectAutomatic(profile, table));

        Assert.Equal(profile.TableFormats[0].Key, selected.Key);
        Assert.Null(table.ReferenceTableFormatKey);
    }

    [Fact]
    public void EffectiveSelection_UsesExplicitStableProfileKey()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var table = CreateTable(columnCount: 6);
        table.ReferenceTableFormatKey = profile.TableFormats[1].Key;

        var selected = Assert.IsType<ReferenceTableFormatProfile>(
            TableFormatProfileSelector.Select(profile, table));

        Assert.Equal(profile.TableFormats[1].Key, selected.Key);
        Assert.Equal(profile.TableFormats[1].Key, table.ReferenceTableFormatKey);
    }

    [Fact]
    public void InvalidLegacyKey_FallsBackToAutomaticSelectionWithoutRewritingAuthoredState()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var table = CreateTable(columnCount: 6);
        table.ReferenceTableFormatKey = "missing-profile";

        var selected = Assert.IsType<ReferenceTableFormatProfile>(
            TableFormatProfileSelector.Select(profile, table));

        Assert.Equal(profile.TableFormats[0].Key, selected.Key);
        Assert.Equal("missing-profile", table.ReferenceTableFormatKey);
    }

    [Fact]
    public void ReferenceAwareResolver_ConsumesTheSameEffectiveProfileSelectedBySharedSelector()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var table = CreateTable(columnCount: 6);
        table.ReferenceTableFormatKey = profile.TableFormats[1].Key;
        var expected = Assert.IsType<ReferenceTableFormatProfile>(
            TableFormatProfileSelector.Select(profile, table));

        var resolved = new ReferenceReportContentFormatResolver().ResolveTable(profile, table);

        Assert.Same(expected.Format, resolved);
    }

    private static TableElement CreateTable(int columnCount)
    {
        var table = new TableElement { Name = "Format selection regression" };
        for (var index = 0; index < columnCount; index++)
            table.Columns.Add(new TableColumn { Header = $"Column {index + 1}" });
        return table;
    }
}
