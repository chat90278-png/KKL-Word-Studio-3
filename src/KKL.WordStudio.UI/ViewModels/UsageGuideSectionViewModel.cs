namespace KKL.WordStudio.UI.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KKL.WordStudio.UI.Models;
using KKL.WordStudio.UI.Services;

public sealed partial class UsageGuideSectionViewModel : ViewModelBase
{
    private readonly GuideImageSourceLoader _imageLoader;
    private readonly string _defaultImageAssetName;

    public UsageGuideSectionViewModel(UsageGuideSection section, GuideImageSourceLoader imageLoader)
    {
        _imageLoader = imageLoader;
        _defaultImageAssetName = section.ImageAssetName;
        _title = section.Title;
        Icon = section.Icon;
        _purpose = section.Purpose;
        _actionsText = string.Join(Environment.NewLine, section.Actions);
        _tip = section.Tip;
        _imageSource = imageLoader.Load(section.ImageAssetName);
    }

    public string Icon { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private string _purpose;
    [ObservableProperty] private string _actionsText;
    [ObservableProperty] private string _tip;
    [ObservableProperty] private ImageSource? _imageSource;
    [ObservableProperty] private string? _customImagePath;

    public IReadOnlyList<string> Actions => ActionsText
        .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    partial void OnActionsTextChanged(string value) => OnPropertyChanged(nameof(Actions));

    [RelayCommand]
    private void ChangeImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Kılavuz ekran görüntüsünü seçin",
            Filter = "Görsel dosyaları|*.png;*.jpg;*.jpeg;*.webp|Tüm dosyalar|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        var imageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KKL Word Studio", "Guide", "Images");
        Directory.CreateDirectory(imageDirectory);
        var extension = Path.GetExtension(dialog.FileName);
        var destination = Path.Combine(imageDirectory, $"{Guid.NewGuid():N}{extension}");
        File.Copy(dialog.FileName, destination, overwrite: true);
        RestoreImage(destination);
    }

    public void RestoreImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        CustomImagePath = path;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        ImageSource = bitmap;
    }

    public void ResetToDefaults(UsageGuideSection section)
    {
        Title = section.Title;
        Purpose = section.Purpose;
        ActionsText = string.Join(Environment.NewLine, section.Actions);
        Tip = section.Tip;
        CustomImagePath = null;
        ImageSource = _imageLoader.Load(_defaultImageAssetName);
    }
}