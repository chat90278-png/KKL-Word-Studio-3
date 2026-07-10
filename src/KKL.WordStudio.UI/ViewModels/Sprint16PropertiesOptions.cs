namespace KKL.WordStudio.UI.ViewModels;

public sealed class TableFormatOptionViewModel
{
    public string? Key { get; init; }
    public required string DisplayName { get; init; }
}

public sealed class GroupingColumnChoiceViewModel
{
    public required Guid ColumnId { get; init; }
    public required string DisplayName { get; init; }
}
