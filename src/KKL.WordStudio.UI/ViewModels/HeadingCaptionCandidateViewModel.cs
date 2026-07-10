namespace KKL.WordStudio.UI.ViewModels;

public sealed class HeadingCaptionCandidateViewModel
{
    public required Guid ElementId { get; init; }
    public required string Text { get; init; }
    public required string Level { get; init; }
    public string DisplayText => $"{Level} · {Text}";
}
