namespace KKL.WordStudio.Domain.Tests;

using KKL.WordStudio.Domain.DataBinding;
using KKL.WordStudio.Domain.DataSources;
using Xunit;

public sealed class Sprint23ColumnMappingMetadataTests
{
    [Fact]
    public void ExistingStyleMapping_DefaultsToIncludedForBackwardCompatibility()
    {
        var mapping = new ColumnMapping
        {
            SourceColumn = "A",
            TargetField = new DataField { Name = "ItemNumber", DataType = "string" }
        };

        Assert.True(mapping.IsIncluded);
        Assert.Null(mapping.DisplayHeader);
        Assert.Null(mapping.SemanticRole);
    }

    [Fact]
    public void DisplayHeader_CanChangeWithoutChangingLogicalFieldIdentity()
    {
        var mapping = new ColumnMapping
        {
            SourceColumn = "B",
            TargetField = new DataField { Name = "PartNameEnglish", DataType = "string" },
            DisplayHeader = "Parça Adı (İngilizce)",
            SemanticRole = "PartNameEnglish",
            IsIncluded = true
        };

        mapping.DisplayHeader = "Parça Adı";

        Assert.Equal("PartNameEnglish", mapping.TargetField.Name);
        Assert.Equal("Parça Adı", mapping.DisplayHeader);
        Assert.Equal("PartNameEnglish", mapping.SemanticRole);
    }
}
