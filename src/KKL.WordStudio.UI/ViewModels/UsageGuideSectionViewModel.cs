namespace KKL.WordStudio.UI.ViewModels;

using System.Windows.Media;
using KKL.WordStudio.UI.Models;
using KKL.WordStudio.UI.Services;

public sealed class UsageGuideSectionViewModel
{
    private readonly GuideImageSourceLoader _imageLoader;
    private ImageSource? _imageSource;

    public UsageGuideSectionViewModel(UsageGuideSection section, GuideImageSourceLoader imageLoader)
    {
        Section = section;
        _imageLoader = imageLoader;
    }

    public UsageGuideSection Section { get; }
    public string Title => Section.Title;
    public string Icon => Section.Icon;
    public string Purpose => Section.Purpose;
    public IReadOnlyList<string> Actions => Section.Actions;
    public string Tip => Section.Tip;
    public ImageSource? ImageSource => _imageSource ??= _imageLoader.Load(Section.ImageAssetName);
}
