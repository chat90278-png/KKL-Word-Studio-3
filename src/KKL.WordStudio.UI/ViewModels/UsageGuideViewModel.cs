namespace KKL.WordStudio.UI.ViewModels;

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KKL.WordStudio.UI.Models;
using KKL.WordStudio.UI.Services;

public sealed partial class UsageGuideViewModel : ViewModelBase
{
    private readonly GuideImageSourceLoader _imageLoader;
    private readonly UsageGuideContentStore _contentStore;
    private string? _editImagePath;
    private bool _useDefaultImage;

    public UsageGuideViewModel(GuideImageSourceLoader imageLoader)
        : this(imageLoader, new UsageGuideContentStore())
    {
    }

    internal UsageGuideViewModel(
        GuideImageSourceLoader imageLoader,
        UsageGuideContentStore contentStore)
    {
        _imageLoader = imageLoader;
        _contentStore = contentStore;

        var overrides = contentStore.LoadOverrides();
        Sections = new ObservableCollection<UsageGuideSectionViewModel>(
            BuildSections().Select(section =>
            {
                overrides.TryGetValue(section.Id, out var contentOverride);
                return new UsageGuideSectionViewModel(
                    section,
                    imageLoader,
                    contentOverride,
                    contentStore.ResolveCustomImagePath(contentOverride));
            }));

        _selectedSection = Sections.FirstOrDefault();
    }

    public ObservableCollection<UsageGuideSectionViewModel> Sections { get; }

    [ObservableProperty] private UsageGuideSectionViewModel? _selectedSection;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editPurpose = string.Empty;
    [ObservableProperty] private string _editActionsText = string.Empty;
    [ObservableProperty] private string _editTip = string.Empty;
    [ObservableProperty] private ImageSource? _editImageSource;
    [ObservableProperty] private string _editorStatusText = string.Empty;

    public void ResetToStart()
    {
        if (IsEditMode) CancelEdits();
        SelectedSection = Sections.FirstOrDefault();
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (SelectedSection is null) return;

        EditTitle = SelectedSection.Title;
        EditPurpose = SelectedSection.Purpose;
        EditActionsText = string.Join(Environment.NewLine, SelectedSection.Actions);
        EditTip = SelectedSection.Tip;
        EditImageSource = SelectedSection.ImageSource;
        _editImagePath = SelectedSection.CustomImagePath;
        _useDefaultImage = SelectedSection.CustomImagePath is null;
        EditorStatusText = "Başlık, açıklamalar, adımlar ve ekran görseli düzenlenebilir.";
        IsEditMode = true;
    }

    [RelayCommand]
    private void ChooseImage()
    {
        var path = _contentStore.PickImage();
        if (path is null) return;

        var image = _imageLoader.LoadFile(path);
        if (image is null)
        {
            EditorStatusText = "Seçilen görsel okunamadı. PNG, JPG veya BMP dosyası seçin.";
            return;
        }

        _editImagePath = path;
        _useDefaultImage = false;
        EditImageSource = image;
        EditorStatusText = "Yeni görsel seçildi. Kalıcı olması için Kaydet düğmesine basın.";
    }

    [RelayCommand]
    private void UseDefaultImage()
    {
        if (SelectedSection is null) return;
        _editImagePath = null;
        _useDefaultImage = true;
        EditImageSource = _imageLoader.LoadEmbedded(SelectedSection.DefaultImageAssetName);
        EditorStatusText = "Bu bölümün uygulamayla gelen yüksek çözünürlüklü görseli seçildi.";
    }

    [RelayCommand]
    private void SaveEdits()
    {
        if (SelectedSection is null) return;

        var title = EditTitle.Trim();
        var purpose = EditPurpose.Trim();
        var tip = EditTip.Trim();
        var actions = EditActionsText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(action => action.Length > 0)
            .ToArray();

        if (title.Length == 0 || purpose.Length == 0 || tip.Length == 0 || actions.Length == 0)
        {
            EditorStatusText = "Başlık, açıklama, en az bir kullanım adımı ve ipucu boş bırakılamaz.";
            return;
        }

        try
        {
            var saved = _contentStore.Save(
                SelectedSection.Id,
                title,
                purpose,
                actions,
                tip,
                _editImagePath,
                _useDefaultImage);

            SelectedSection.ApplyOverride(saved, _contentStore.ResolveCustomImagePath(saved));
            IsEditMode = false;
            EditorStatusText = "Değişiklikler bu Windows kullanıcısı için kaydedildi.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            EditorStatusText = $"Kılavuz kaydedilemedi: {exception.Message}";
        }
    }

    [RelayCommand]
    private void CancelEdits()
    {
        IsEditMode = false;
        EditorStatusText = "Düzenlenmemiş değişiklikler iptal edildi.";
    }

    [RelayCommand]
    private void ResetSection()
    {
        if (SelectedSection is null) return;

        try
        {
            _contentStore.Reset(SelectedSection.Id);
            SelectedSection.ResetToDefault();
            IsEditMode = false;
            EditorStatusText = "Seçili bölüm varsayılan metin ve görsele döndürüldü.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EditorStatusText = $"Varsayılana dönülemedi: {exception.Message}";
        }
    }

    partial void OnSelectedSectionChanged(UsageGuideSectionViewModel? value)
    {
        if (IsEditMode) CancelEdits();
    }

    private static IReadOnlyList<UsageGuideSection> BuildSections() =>
    [
        new("baslangic", "Başlangıç Ekranı", "⌂", "01-ana-ekran-bos.jpg",
            "Uygulamanın ana çalışma alanını ve temel komutlarını tanıtır.",
            ["Excel ekle düğmesiyle kaynak dosyanızı seçin veya dosyayı çalışma alanına sürükleyin.", "Yüklenen kaynakları üst bölümden takip edin.", "Rapor alanını sağ kenardaki ok düğmesiyle açıp kapatın.", "Rapor hazır olduğunda Word Dosyası Oluştur düğmesini kullanın."],
            "Kaynak Excel dosyası doğrudan değiştirilmez; uygulama kendi çalışma verisi üzerinde işlem yapar."),

        new("excel-kaynagi", "Excel Kaynağı ve Sayfa Seçimi", "▦", "02-demo-excel-yuklu.jpg",
            "Yüklenen Excel dosyasındaki çalışma sayfalarını görüntülemek, veriyi incelemek ve rapora aktarılacak sütunları seçmek için kullanılır.",
            ["Alt sekmelerden çalışmak istediğiniz sayfayı seçin.", "Tablodaki başlıkları ve veri satırlarını kontrol edin.", "Sütun harflerinin yanındaki kutularla rapora aktarılacak sütunları belirleyin.", "Durum satırında otomatik algılanan veri aralığını kontrol edin."],
            "Ekran görüntüsündeki örnek kayıtlar yalnızca arayüzü göstermek içindir; kendi Excel dosyanız farklı sütunlar içerebilir."),

        new("veri-araligi", "Veri Aralığını Düzenle", "↔", "16-veri-araligi-birlesik.jpg",
            "Başlık satırını, veri başlangıç ve bitiş satırlarını ve kullanılacak sütun sınırlarını belirler.",
            ["Veri Aralığını Düzenle düğmesine basın.", "Dosyanızda başlık satırı varsa Başlık satırı var seçeneğini açık bırakın.", "Başlangıç ve bitiş satırlarıyla sütun sınırlarını kontrol edin.", "Yeniden Algıla ile otomatik tespit yapın veya Uygula ile girdiğiniz aralığı kullanın."],
            "Yanlış aralık seçimi bazı kayıtların eksik kalmasına veya boş satırların rapora eklenmesine neden olabilir."),

        new("worde-aktar", "Word'e Aktar", "→", "03-worde-aktar-yerlesim-onayi.jpg",
            "Seçili Excel verisini rapora başlık, alt başlık ve tablo yapısı olarak ekler veya mevcut bir tabloyu günceller.",
            ["Aktarılacak sütunları seçtikten sonra Word'e Aktar düğmesine basın.", "Yeni tablo ekleme veya mevcut tabloyu güncelleme seçeneğini belirleyin.", "Başlık, alt başlık ve tablo adını düzenleyin.", "Onayla ve Önizle düğmesiyle içeriği rapora ekleyin."],
            "Başlık veya alt başlık kullanmayacaksanız ilgili satırın yanındaki eksi düğmesiyle bu katmanı kaldırabilirsiniz."),

        new("icindekiler-preview", "İçindekiler ve Önizleme", "☷", "04-preview-ve-icindekiler.jpg",
            "Rapor yapısını düzenlemek ve Word çıktısından önce sayfa görünümünü kontrol etmek için kullanılır.",
            ["İçindekiler panelinden başlık, alt başlık, tablo, üst bilgi veya alt bilgi ekleyin.", "Seçili öğeyi yeniden adlandırın, silin, taşıyın veya girinti düzeyini değiştirin.", "Bir öğeye çift tıklayarak önizlemedeki yerine gidin.", "Sayfa sayısını, başlık sırasını ve tablo yerleşimini önizlemeden kontrol edin."],
            "Önizleme, Word çıktısının düzenini işlem tamamlanmadan önce görmenizi sağlar."),

        new("tablo-ozellikleri", "Tablo Özellikleri", "⚙", "05-tablo-ozellikleri.jpg",
            "Seçili tablonun adını, başlığını, biçimini, veri kaynaklarını ve görünüm ayarlarını yönetir.",
            ["İçindekiler panelinden tabloyu seçip Özellikler sekmesine geçin.", "Tablo adını, açıklamasını ve başlık metnini düzenleyin.", "Tablo biçimini ve bağlı veri kaynaklarını kontrol edin.", "Uygulamanın algıladığı özel sütun düzenini gerekiyorsa düzenleyin veya kaldırın.", "Uygula düğmesiyle önizlemeyi yenileyin."],
            "Yaptığınız değişiklikleri uyguladıktan sonra tablonun sayfa üzerindeki görünümünü önizlemeden kontrol edin."),

        new("baslik-ozellikleri", "Başlık Özellikleri", "A", "06-baslik-ozellikleri.jpg",
            "Seçili rapor başlığının metnini ve hiyerarşi düzeyini değiştirir.",
            ["İçindekiler ağacından bir başlık seçin.", "Özellikler sekmesinde başlık metnini düzenleyin.", "Başlık 1 veya Başlık 2 düzeyini seçin.", "Uygula düğmesiyle numaralandırmayı ve önizlemeyi yenileyin."],
            "Başlık düzeyi, raporun bölüm sırasını ve numaralandırmasını doğrudan etkiler."),

        new("uyarilar", "Uyarılar", "!", "07-uyarilar.jpg",
            "Rapor hazırlanırken bulunan hata, uyarı ve bilgi mesajlarını gösterir.",
            ["Uyarılar sekmesini açın.", "Gerekirse Hata, Uyarı veya Bilgi filtrelerinden birini seçin.", "Bir uyarı kartına tıklayarak ilgili rapor öğesine veya Excel hücresine gidin.", "Sorunu düzelttikten sonra bulgu sayısının güncellendiğini kontrol edin."],
            "Hata seviyesindeki bulgular Word çıktısını engelleyebilir; diğer uyarılar çıktıdan önce gözden geçirilmelidir."),

        new("hizli-rapor", "Hızlı Rapor", "⚡", "17-hizli-rapor-birlesik.jpg",
            "Bir veya daha fazla çalışma sayfasından rapor yapısını tek işlemde oluşturur.",
            ["Hızlı Rapor düğmesine basın.", "Rapora eklenecek çalışma sayfalarını seçin.", "Sayfaların sırasını yukarı ve aşağı düğmeleriyle düzenleyin.", "Başlık, alt başlık ve tablo adlarını kontrol edin.", "Rapor Yapısını Oluştur düğmesiyle seçilen yapıları rapora ekleyin."],
            "Az sayıda veri için normal Word'e Aktar akışı, çok sayıda çalışma sayfası için Hızlı Rapor daha pratiktir."),

        new("word-ciktisi", "Word Dosyası Oluştur", "W", "11-word-ciktisi.jpg",
            "Hazırlanan raporu gerçek bir .docx dosyası olarak kaydeder.",
            ["Önizlemeyi ve varsa uyarıları kontrol edin.", "Word Dosyası Oluştur düğmesine basın.", "Kayıt konumunu ve dosya adını seçin.", "İşlem tamamlanınca Dosyayı Aç veya Klasörde Göster bağlantısını kullanın.", "Oluşan belgeyi Microsoft Word'de açarak son görünümü kontrol edin."],
            "Word dosyası ayrı bir çıktı olarak oluşturulur; kaynak Excel dosyanız değiştirilmez.")
    ];
}
