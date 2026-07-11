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

    [Fact]
    public void AutomaticBuiltInFormat_WidensLongLeadingHeaderWithoutChangingTotalWeight()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var table = CreateTable(
            "Parça Adı (İngilizce)",
            "Parça Adı (Türkçe)",
            "Parça No",
            "Ünite Yapısal Altı",
            "Parça No REF",
            "NSN");
        var baseFormat = profile.TableFormats[0].Format;

        var resolved = new ReferenceReportContentFormatResolver().ResolveTable(profile, table);

        Assert.NotSame(baseFormat, resolved);
        Assert.True(resolved.Columns[0].WidthWeight >= 14d);
        Assert.Equal(
            baseFormat.Columns.Sum(column => column.WidthWeight),
            resolved.Columns.Sum(column => column.WidthWeight),
            precision: 6);
        Assert.Null(table.ReferenceTableFormatKey);
    }

    [Fact]
    public void ExplicitBuiltInFormat_RemainsExactEvenWithLongLeadingHeader()
    {
        var profile = DefaultDocumentFormatProfileFactory.Create();
        var table = CreateTable(
            "Parça Adı (İngilizce)",
            "Parça Adı (Türkçe)",
            "Parça No",
            "Ünite Yapısal Altı",
            "Parça No REF",
            "NSN");
        table.ReferenceTableFormatKey = profile.TableFormats[0].Key;

        var resolved = new ReferenceReportContentFormatResolver().ResolveTable(profile, table);

        Assert.Same(profile.TableFormats[0].Format, resolved);
    }

    private static TableElement CreateTable(int columnCount)
    {
        var table = new TableElement { Name = "Format selection regression" };
        for (var index = 0; index < columnCount; index++)
            table.Columns.Add(new TableColumn { Header = $"Column {index + 1}" });
        return table;
    }

    private static TableElement CreateTable(params string[] headers)
    {
        var table = new TableElement { Name = "Header-aware format regression" };
        foreach (var header in headers)
            table.Columns.Add(new TableColumn { Header = header });
        return table;
    }
}
