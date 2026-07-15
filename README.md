# KKL Word Studio

Excel kaynaklarından düzenlenebilir rapor yapısı oluşturup gerçek `.docx` çıktısı üreten .NET 8 WPF masaüstü uygulaması.

## Güncel ürün akışı

1. Bir veya daha fazla `.xlsx` / `.xlsm` dosyası açılır.
2. Veri aralığı otomatik algılanır; gerekirse kullanıcı tarafından düzenlenir.
3. Aktarılacak sütunlar Excel başlıklarındaki checkbox'larla seçilir ve görünür sütun sırası korunur.
4. Normal `Word'e Aktar` veya Hızlı Rapor ile Başlık → Alt Başlık → Tablo yapısı oluşturulur.
5. İçindekiler, özellikler, çok sayfalı Preview ve structured Uyarılar alanı aynı oturum modelini kullanır.
6. Word export preflight, engelleyici Error varsa çıktıyı durdurur; Warning/Information bulgularında export'a izin verir.
7. Mevcut Word exporter aynı rapor içeriğini `.docx` olarak üretir.

## Oturum modeli

KKL Word Studio artık native proje dosyası açmaz veya kaydetmez.

- Uygulama başlangıcında bir adet process-lifetime in-memory çalışma oturumu oluşturulur.
- Excel kaynakları, çalışma verisi, rapor yapısı, ön belge ve biçim şablonu bu oturum içinde yaşar.
- Uygulama kapatıldığında oturum sona erer.
- `.kws`, proje aç/kaydet, recent-project ve repository yaşam döngüsü production kodunda bulunmaz.
- Ön belge ve biçim şablonu kaynak DOCX'leri yalnızca read-only açılır; uygulama bunları yeniden yazmaz.

## Çözüm sınırları

| Proje | Sorumluluk |
|---|---|
| `KKL.WordStudio.Shared` | Sonuç tipi, guard/extension yardımcıları, framework-bağımsız geometri ve ortak sabitler |
| `KKL.WordStudio.Domain` | Rapor, sayfa, bölüm, element, veri kaynağı ve çalışma verisi modelleri; I/O içermez |
| `KKL.WordStudio.Application` | Workspace, transfer, report-content, structured diagnostics, format ve export kontratları |
| `KKL.WordStudio.Engine` | Deterministik belge yerleşimi, sayfalama ve tablo parçalama |
| `KKL.WordStudio.Infrastructure` | Read-only Excel/DOCX okuyucuları, veri sağlayıcıları ve gerçek Word exporter |
| `KKL.WordStudio.Rendering` | Design-surface etkileşimi, hit-test, seçim, snapping ve zoom |
| `KKL.WordStudio.UI` | WPF shell, ViewModel'ler ve composition root |

Temel kural: UI yerleşim veya business hesabı yapmaz; Preview ve Word aynı report-content ve sayfalama kararlarını tüketir. İkinci Excel okuyucu, renderer, paginator veya Word exporter oluşturulmaz.

## Tamamlanmış ana yetenekler

- Excel drag/drop, çoklu workbook ve worksheet seçimi
- Otomatik/manüel veri aralığı
- Project-owned çalışma verisi düzenleme, undo/redo, find/replace, satır/sütun işlemleri
- Sütun seçimi ve görünür sıra ile Word'e aktarım
- Normal aktarım ve Hızlı Rapor için ortak transfer motoru
- Korunan ana başlık, başlık/alt başlık numaralandırması ve tablo caption'ları
- İçindekiler düzenleme ve stable-ID Preview navigasyonu
- Deterministik çok sayfalı A4 Preview
- Heading/AltHeading/Table zinciri ve caption/header/table-start sayfalama kuralları
- Uzun tablolarda tekrarlanan header, satır `CantSplit` ve Preview–Word pagination parity
- Structured diagnostic kodları, gruplama, doğru Excel hücresine navigasyon ve düzenleme sonrası otomatik yeniden hesaplama
- Word export preflight ve gerçek OpenXML DOCX üretimi
- Read-only ön belge ve reference-format DOCX kullanımı

## Bilinen sonraki işler

- Büyük gerçek Excel dosyaları ve çok sayfalı Word çıktıları için geniş test matrisi
- Çok uzun hücre metni, birleşik hücre ve farklı font metriklerinde Preview–Word uç durumlarını azaltma
- PDF/HTML/Image/Excel exporter stub'larını ürün kararına göre ele alma
- `Binding.Filter` expression çalıştırma ve düzenleme UI'sı
- Büyük worksheet'ler için gerekirse OpenXML streaming okuyucu
- Final performans, hata yönetimi ve release stabilizasyonu

## Derleme ve çalıştırma

```bat
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

UI projesi WPF kullandığı için Windows üzerinde çalıştırılmalıdır. `docs/adr/` ve eski sprint raporları tarihsel karar kaydıdır; geçmişteki native proje akışını anlatan bölümler güncel ürün kontratı değildir.
