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

    public UsageGuideViewModel(
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

    [ObservableProperty]
    private UsageGuideSectionViewModel? _selectedSection;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private string _editPurpose = string.Empty;

    [ObservableProperty]
    private string _editActionsText = string.Empty;

    [ObservableProperty]
    private string _editTip = string.Empty;

    [ObservableProperty]
    private ImageSource? _editImageSource;

    [ObservableProperty]
    private string _editorStatusText = string.Empty;

    public void ResetToStart()
    {
        if (IsEditMode)
            CancelEdits();
        SelectedSection = Sections.FirstOrDefault();
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (SelectedSection is null)
            return;

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
        if (path is null)
            return;

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
        if (SelectedSection is null)
            return;

        _editImagePath = null;
        _useDefaultImage = true;
        EditImageSource = _imageLoader.LoadEmbedded(SelectedSection.DefaultImageAssetName);
        EditorStatusText = "Bu bölümün uygulamayla gelen yüksek çözünürlüklü görseli seçildi.";
    }

    [RelayCommand]
    private void SaveEdits()
    {
        if (SelectedSection is null)
            return;

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
        if (SelectedSection is null)
            return;

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
        if (IsEditMode)
            CancelEdits();
    }

    private static IReadOnlyList<UsageGuideSection> BuildSections() =>
    [
        new(
            "baslangic",
            "Başlangıç Ekranı",
            "⌂",
            "01-ana-ekran-bos.jpg",
            "Excel kaynağı yüklenmeden önce uygulamanın Excel çalışma alanını, rapor önizlemesini ve sağ bağlam panelini gösterir.",
            [
                "Excel ekle düğmesiyle .xlsx veya .xlsm uzantılı bir kaynak seçin.",
                "Alternatif olarak Excel dosyasını boş kaynak alanına sürükleyip bırakın.",
                "Sağ kenardaki ok düğmesiyle rapor çalışma alanını açıp kapatın.",
                "Word Dosyası Oluştur düğmesini rapor tamamlandıktan sonra kullanın."
            ],
            "Kaynak Excel dosyası doğrudan değiştirilmez; hücre düzenlemeleri uygulamanın çalışma verisinde tutulur."),
        new(
            "excel-kaynagi",
            "Excel Kaynağı ve Worksheet",
            "▦",
            "02-demo-excel-yuklu.jpg",
            "Yüklenen workbook içindeki worksheet verisini incelemek, sütun sırasını görmek ve rapora aktarılacak sütunları seçmek için kullanılır.",
            [
                "Alt sekmelerden Parca_Listesi, Uyari_Senaryolari veya Uzun_Parca_Listesi worksheet'ini seçin.",
                "No, Tr İsim, Parça Numarası, NSN, Seri Numarası ve Adet sütunlarını kontrol edin.",
                "Sütun harflerinin yanındaki kutularla aktarılacak sütunları belirleyin.",
                "Durum satırında otomatik algılanan veri aralığını doğrulayın."
            ],
            "Sütunların ekrandaki görünür sırası Preview ve Word çıktısında korunur."),
        new(
            "veri-araligi",
            "Veri Aralığını Düzenle",
            "↔",
            "16-veri-araligi-birlesik.jpg",
            "Başlık satırı ile veri başlangıç/bitiş satırlarını ve A:F sütun sınırlarını doğrulamak için kullanılır.",
            [
                "Veri Aralığını Düzenle düğmesine basın.",
                "Başlık satırı var seçeneğini kontrol edin.",
                "Demo dosyada başlık satırını 2, veri başlangıcını 3 ve bitiş sütununu F olarak doğrulayın.",
                "Yeniden Algıla otomatik tespit yapar; Uygula girilen sınırları çalışma alanına taşır."
            ],
            "Yanlış aralık seçimi eksik kayıtların veya gereksiz boş satırların rapora taşınmasına neden olabilir."),
        new(
            "worde-aktar",
            "Word'e Aktar",
            "→",
            "03-worde-aktar-yerlesim-onayi.jpg",
            "Seçili worksheet verisini başlık, alt başlık ve tablo yapısı olarak rapora ekler veya mevcut tabloyu günceller.",
            [
                "Aktarılacak sütunları seçtikten sonra Word'e Aktar düğmesine basın.",
                "Yeni tablo olarak ekle veya mevcut tabloyu güncelle seçeneğini belirleyin.",
                "Başlık, alt başlık ve tablo adını onaydan önce düzenleyin.",
                "Onayla ve Önizle düğmesiyle aynı işlemi rapor ağacına ve Preview'a uygulayın."
            ],
            "Başlık ya da alt başlık gerekmiyorsa ilgili satırın yanındaki eksi düğmesiyle katmanı kaldırabilirsiniz."),
        new(
            "icindekiler-preview",
            "İçindekiler ve Preview",
            "☷",
            "04-preview-ve-icindekiler.jpg",
            "Rapor ağacını yönetir ve oluşturulan sayfaları Word'e aktarılmadan önce gerçek zamanlı olarak gösterir.",
            [
                "Başlık, Alt Başlık, Tablo, Üst Bilgi veya Alt Bilgi ekleyin.",
                "Seçili öğeyi yeniden adlandırın, silin, yukarı/aşağı taşıyın veya girintileyin.",
                "İçindekiler öğesine çift tıklayarak Preview'daki gerçek öğeye gidin.",
                "Sayfa sayısını ve tablo başlangıç konumunu Preview üzerinden kontrol edin."
            ],
            "Yeşil durum noktası tablonun veri kaynağına bağlı ve önizlemeye hazır olduğunu gösterir."),
        new(
            "tablo-ozellikleri",
            "Tablo Özellikleri",
            "⚙",
            "05-tablo-ozellikleri.jpg",
            "Seçili tablonun adı, caption metni, biçimi, veri kaynakları, başlık satırı ve Seri No / Adet düzenini yönetir.",
            [
                "İçindekiler panelinden tabloyu seçip Özellikler sekmesine geçin.",
                "Tablo adını, açıklamayı ve Tablo N: başlığında kullanılacak metni düzenleyin.",
                "Tablo biçimini seçin ve bağlı veri kaynaklarını kontrol edin.",
                "Seri No / Adet düzenini otomatik algılayın veya Düzenle düğmesiyle değiştirin.",
                "Uygula düğmesiyle Preview'ı yenileyin."
            ],
            "Aynı Parça Numarasına ait birden fazla Seri Numarası tek rapor satırında gruplanabilir; Adet değeri bu yapıya göre gösterilir."),
        new(
            "baslik-ozellikleri",
            "Başlık Özellikleri",
            "A",
            "06-baslik-ozellikleri.jpg",
            "Seçili rapor başlığının metnini ve Başlık 1 / Başlık 2 düzeyini değiştirir.",
            [
                "İçindekiler ağacında bir başlık seçin.",
                "Özellikler sekmesinde Metin alanını düzenleyin.",
                "Başlık 1 veya Başlık 2 düzeyini seçin.",
                "Uygula düğmesiyle numaralandırmayı ve Preview'ı yenileyin."
            ],
            "Başlık düzeyi 1, 1.1 ve 1.1.1 numaralandırmasını ve rapor hiyerarşisini doğrudan etkiler."),
        new(
            "uyarilar",
            "Uyarılar",
            "!",
            "07-uyarilar.jpg",
            "Rapor verisindeki hata, uyarı ve bilgi bulgularını sorun türüne göre gruplayarak gösterir.",
            [
                "Uyarılar sekmesini açın ve Tümü, Hata, Uyarı veya Bilgi filtresini kullanın.",
                "Uyarı kartına tıklayarak ilgili rapor öğesine ve gerçek Excel hücresine gidin.",
                "Sorunlu hücreyi düzelttikten sonra açık bulgu sayısının yenilendiğini kontrol edin.",
                "Word çıktısından önce engelleyici Error bulgularını giderin."
            ],
            "Error seviyesindeki bulgular Word çıktısını engeller; Warning ve Information seviyeleri çıktıya izin verir."),
        new(
            "hizli-rapor",
            "Hızlı Rapor",
            "⚡",
            "17-hizli-rapor-birlesik.jpg",
            "Bir veya daha fazla worksheet için Başlık - Alt Başlık - Tablo yapısını tek işlemde oluşturur.",
            [
                "Hızlı Rapor düğmesine basın ve kaynak worksheet'leri seçin.",
                "Seçim sırasını yukarı/aşağı düğmeleriyle düzenleyin.",
                "Her yapı için başlık, alt başlık ve tablo adlarını değiştirin.",
                "Rapor Yapısını Oluştur düğmesiyle tüm blokları aynı transfer motorundan geçirin."
            ],
            "Normal Word'e Aktar ve Hızlı Rapor aynı transfer motorunu kullandığı için sütun sırası ve veri aralığı davranışları tutarlıdır."),
        new(
            "word-ciktisi",
            "Word Dosyası Oluştur",
            "W",
            "11-word-ciktisi.jpg",
            "Preview ile hazırlanan raporu gerçek .docx dosyası olarak üretir ve Microsoft Word'de son kontrol yapılmasını sağlar.",
            [
                "Word Dosyası Oluştur düğmesine basın.",
                "Kayıt konumunu ve dosya adını seçin.",
                "İşlem tamamlanınca durum çubuğundaki Dosyayı Aç veya Klasörde Göster bağlantısını kullanın.",
                "Başlıkları, tablo caption'ını, devam sayfalarını ve seri/adet gruplamasını Word'de kontrol edin."
            ],
            "Uzun tablolarda kolon başlıkları devam sayfalarında tekrarlanır ve mümkün olan satırların sayfalar arasında ortadan bölünmesi önlenir.")
    ];
}
