namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using KKL.WordStudio.UI.Models;
using KKL.WordStudio.UI.Services;

public sealed partial class UsageGuideSectionViewModel : ViewModelBase
{
    private readonly UsageGuideSection _defaults;
    private readonly GuideImageSourceLoader _imageLoader;

    public UsageGuideSectionViewModel(
        UsageGuideSection section,
        GuideImageSourceLoader imageLoader,
        UsageGuideSectionOverride? contentOverride,
        string? customImagePath)
    {
        _defaults = section;
        _imageLoader = imageLoader;
        _title = section.Title;
        _purpose = section.Purpose;
        _actions = new ObservableCollection<string>(section.Actions);
        _tip = section.Tip;
        _imageSource = imageLoader.LoadEmbedded(section.ImageAssetName);

        if (contentOverride is not null)
            ApplyOverride(contentOverride, customImagePath);
    }

    public string Id => _defaults.Id;
    public string Icon => _defaults.Icon;
    public string DefaultImageAssetName => _defaults.ImageAssetName;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _purpose;

    [ObservableProperty]
    private ObservableCollection<string> _actions;

    [ObservableProperty]
    private string _tip;

    [ObservableProperty]
    private ImageSource? _imageSource;

    [ObservableProperty]
    private string? _customImagePath;

    public void ApplyOverride(UsageGuideSectionOverride contentOverride, string? customImagePath)
    {
        Title = contentOverride.Title;
        Purpose = contentOverride.Purpose;
        Actions = new ObservableCollection<string>(contentOverride.Actions);
        Tip = contentOverride.Tip;
        CustomImagePath = customImagePath;
        ImageSource = customImagePath is null
            ? _imageLoader.LoadEmbedded(_defaults.ImageAssetName)
            : _imageLoader.LoadFile(customImagePath) ?? _imageLoader.LoadEmbedded(_defaults.ImageAssetName);
    }

    public void ResetToDefault()
    {
        Title = _defaults.Title;
        Purpose = _defaults.Purpose;
        Actions = new ObservableCollection<string>(_defaults.Actions);
        Tip = _defaults.Tip;
        CustomImagePath = null;
        ImageSource = _imageLoader.LoadEmbedded(_defaults.ImageAssetName);
    }
}
