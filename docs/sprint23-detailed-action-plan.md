# KKL Word Studio — Sprint 23 Detaylı Harekât Planı

**Plan tarihi:** 13 Temmuz 2026  
**Repository:** `chat90278-png/KKL-Word-Studio-3`  
**Kod baseline:** `aa51d76850ce77596ba4e37a64559a926b11629a`  
**Tranche branch başlangıcı:** `main@8f7fd9781dd51a2ebded5214a6568154e0aea075`  
**Başlangıç test toplamı:** `514/514`

> `main` üzerindeki `aa51d768...` kod ağacı Sprint 22 GREEN baseline'dır. Plan yazımı sırasında yanlışlıkla oluşturulan tek satırlık doküman hemen sonraki committe silindi; `8f7fd978...` kod ağacı `aa51d768...` ile aynıdır.

## 1. Hedef

Sprint 23; Excel kaynağını daha güvenilir anlayan, standart Word tablosu üreten, rapor yapısını kolay düzenleten, küçük ekranlarda rahat çalışan, Ön Belgeyi değiştirmeden birleştiren ve yalnızca anlamlı uyarılar gösteren tek bütünleşik akış oluşturacaktır.

Mimari sınırlar:

- ikinci Excel okuyucu yok;
- ikinci renderer/Preview hattı yok;
- ikinci Word exporter yok;
- kalıcı ikinci rapor ağacı yok;
- Windows doğrulaması nihai gerçektir;
- exact head kullanıcı tarafından doğrulanmadan GREEN denmez.

## 2. Öncelikler

### P0 — doğruluk

- başlık/veri başlangıcı/veri bitişi algılama;
- manuel veri aralığının kaynak ve worksheet bazında saklanması;
- gerçek Excel sütun harflerinin korunması;
- düzenlenen başlık hücresinin Preview ve Word'e taşınması;
- anlamsal sütun tespiti ve sabit Word sütun sırası;
- var olan tabloyu güncelle/yeni tablo ekle kararı;
- Word'e Aktar öncesi düzenlenebilir başlık/alt başlık/tablo yerleşim onayı;
- kök başlık ve hiyerarşik numaralandırma;
- tablo sonrası sonraki başlığın yeni sayfada başlaması;
- Ön Belgenin salt okunur gösterimi ve birleşik DOCX.

### P1 — kullanılabilirlik

- sütun aktar seçiminin ana gride taşınması;
- sütun sıralamasının kaldırılması;
- drag/drop ve Yukarı/Aşağı mantığının düzeltilmesi;
- İçindekilerden Preview'a çift tık navigasyonu;
- kaynak veri zoom, Ctrl+F ve 100+ satır gezinme;
- sağdan açılır rapor paneli;
- üst banner sadeleştirmesi.

### P2 — kalite

- uyarı seviyeleri, tekrar önleme, yaşam döngüsü ve hedef navigasyonu;
- görsel polish ve düşük riskli kullanım iyileştirmeleri.

## 3. Tranche sırası

### 23-00 — Contract Map ve Karakterizasyon

Branch: `sprint23/00-contract-characterization`

- bu plan repoya alınır;
- mevcut davranışlar testlerle kilitlenir;
- doğrudan transferin henüz yerleşim onayı göstermediği karakterize edilir;
- üretim davranışı değiştirilmez;
- değişecek kontratlar listelenir.

### 23-01 — Excel Range Intelligence

- kanonik alan rolleri: No, İngilizce/Türkçe Parça Adı, Parça Numarası, NSN, Seri Numarası, Adet;
- büyük/küçük harf, Türkçe karakter, nokta/tire/boşluk ve eş anlamlı başlık normalizasyonu;
- İngilizce parça adı varsayılanı;
- ilk boş satırda kesilmeyen veri bitişi;
- biçimlendirilmiş boş satırları veri saymama;
- manuel aralığı proje worksheet'inde saklama;
- sabit 100 satır sınırını kaldırmaya uygun veri erişimi.

### 23-02 — Grid, Sütun Seçimi ve Transfer Standardı

- A/B/C sütun harfleri korunur;
- kaynak başlık satırı normal hücre satırı olarak kalır;
- başlık hücresi düzenleme hatası düzeltilir;
- DataGrid sorting kapatılır;
- aktar checkbox'ları grid başlığına taşınır;
- normal `Sütunları Eşle` drawer'ı kaldırılır;
- `Veri Aralığını Düzenle` üst kaynak barına taşınır;
- Word sütun sırası sabitlenir: No, Parça Adı, Parça Numarası, NSN, Seri Numarası, Adet;
- `Var olan <ad/numara> tablosunu güncelle` ve `Yeni tablo olarak ekle` seçenekleri;
- `Tablo N: [ad]` girişi;
- transfer isteği bu aşamada doğrudan raporu değiştirmez, bir yerleşim taslağı üretir.

### 23-02B — Word'e Aktar Yerleşim Onayı

Bu iş 23-02 transfer kontratı ile 23-03 numaralandırma servisinin ortak entegrasyon noktasıdır. Ayrı rapor motoru veya ikinci ağaç oluşturulmaz.

Popup yalnız ilgili parent zincirini gösterir:

```text
1. System Test Procedure Configuration List
└─ 1.2  [Yeni başlık                         ] [−]
   └─ 1.2.1  [Yeni alt başlık                ] [−]
      └─ Tablo 5: [Tablo adı                 ]

                         [İptal] [Onayla ve Önizle]
```

Kurallar:

- kök parent her zaman gösterilir ve bu popup'tan kaldırılamaz;
- aradaki diğer eş seviyeli başlıklar ve tablolar gösterilmez;
- yalnız hedefe ait parent zinciri gösterilir;
- önerilen yeni başlık, yeni alt başlık ve tablo adı düzenlenebilir;
- başlık ve alt başlık yanında kaldırma düğmesi vardır;
- alt başlık kaldırılırsa tablo yeni başlığın altında kalır;
- başlık kaldırılırsa alt başlık mevcut parent altında yeniden seviyelendirilir;
- ikisi de kaldırılırsa tablo doğrudan gösterilen mevcut parent altına eklenir;
- görüntülenen `1.2`, `1.2.1`, `Tablo 5` değerleri gerçek rapor sırasından hesaplanır, sabit örnek metin değildir;
- tablo seçiliyse mevcut parent zinciri korunur;
- mevcut tablo güncelleniyorsa tablo sessizce başka parent'a taşınmaz;
- seçim yoksa sabit kök parent kullanılır;
- `İptal` hiçbir rapor mutation'ı yapmaz;
- `Onayla ve Önizle` bütün değişiklikleri atomik uygular;
- başarıdan sonra rapor paneli açılır, Preview yenilenir ve hedef tabloya gidilir;
- eski transfer karar popup'ı ile ikinci bir modal zinciri oluşturulmaz; güncelle/yeni tablo/başlık yapısı/tablo adı tek akışta birleştirilir.

### 23-03 — Belge İskeleti ve Numaralandırma

- gerçek, düzenlenebilir fakat silinemez kök heading;
- varsayılan metin `System Test Procedure Configuration List`;
- `1`, `1.1`, `1.1.1` numaralandırması;
- Contents, Preview, Word ve transfer yerleşim popup'ı aynı resolver'ı kullanır;
- tablo seçiliyken Başlık tablonun üstüne;
- tablo seçiliyken Alt Başlık tablo ile sahibi başlık arasına;
- seçim yoksa güvenli sona ekleme;
- Yukarı/Aşağı yalnız aynı seviyedeki mantıksal kardeş bloklarla çalışır;
- drag/drop Before/Into/After göstergeleri belirginleşir;
- yerleşim onay taslağı onaylandıktan sonra tek atomik structure operasyonuyla uygulanır.

### 23-04 — İçindekiler Navigasyonu ve Sayfalama

- çift tıklanan Contents node gerçek element ID ile Preview fragmentine gider;
- panel kapalıysa açılır;
- eski sayfa numarası cache'i kullanılmaz;
- tablo bittikten sonraki heading gerçek page-break ile yeni sayfada başlar;
- Preview ve Word aynı sayfalama politikasını kullanır.

### 23-05 — Responsive Shell

- Excel ana alan;
- sağdan açılan rapor workspace;
- rapor workspace içinde Preview + Context Dock;
- başarılı Word aktarımı, rapor öğesi ekleme ve Contents navigasyonu paneli açar;
- `Yeni` ve `Aç` görünür üst bardan kaldırılır;
- Kaydet görünür, Farklı Kaydet ikincil menüde;
- Excel zoom ve Ctrl+F eklenir.

### 23-06 — Salt Okunur Ön Belge

- Ön Belge içeriği rapor elementlerine dönüştürülmez;
- Preview'da salt okunur sayfa görüntüsü olarak önde görünür;
- inline edit/drag/drop/structure interaction yoktur;
- final DOCX: orijinal Ön Belge + üretilen KKL raporu;
- style, numbering, media, relationship ve section çakışmaları güvenli remap edilir;
- Ön Belge başlıkları KKL İçindekiler'e analiz edilerek eklenmez.

### 23-07 — Uyarı Politikası

- Hata/Uyarı/Bilgi seviyeleri;
- bilgi mesajları sayacı şişirmez;
- kararlı diagnostic kimliği ile tekrar önleme;
- sorun çözülünce otomatik temizleme;
- kaynak/worksheet/element/hücre hedef navigasyonu;
- transfer/export engelleme yalnız gerçek blocker hatalarda.

### 23-08 — Entegre Stabilizasyon

- 10.000+ satır ve sparse workbook;
- farklı sütun sıraları ve Türkçe/İngilizce başlıklar;
- manuel aralık geçiş/persistence;
- başlık hücresi düzenleme → Preview → Word;
- yeni/güncel tablo ve yerleşim onayı;
- başlık/alt başlık kaldırma varyasyonları;
- rapor ağacı, Contents navigasyonu ve page-break;
- küçük ekran paneli;
- Ön Belge birleşimi;
- uyarı yaşam döngüsü;
- single-EXE smoke.

## 4. Bağımlılık sırası

```text
23-00 Contract/karakterizasyon
  → 23-01 Range + semantic field detection + persistence
  → 23-02 Grid + column selection + fixed Word schema
  → 23-02B Transfer placement draft/confirmation
  → 23-03 Root heading + structure + numbering
  → 23-04 TOC navigation + pagination parity
  → 23-05 Responsive shell + zoom/search
  → 23-06 Front Matter readonly merge
  → 23-07 Warning policy
  → 23-08 Integrated stabilization
```

Transfer popup'ı 23-02'de taslak üretmeye başlar; gerçek numaralandırma ve atomik yerleştirme 23-03'ün ortak resolver/structure servisiyle tamamlanır.

Uyarı sistemi sona yakın uygulanır; doğru uyarı senaryoları ancak veri, transfer, rapor ve Preview davranışları stabilize olduğunda kurulabilir.

## 5. Test tahmini

Başlangıç: `514`

Planlanan minimum ek testler:

- 23-00: `+11`
- 23-01: `+22`
- 23-02 ve 23-02B: `+28`
- 23-03: `+24`
- 23-04: `+22`
- 23-05: `+12`
- 23-06: `+22`
- 23-07: `+18`
- 23-08: `+10`

Geçici final tahmini: `683`. Her tranche gerçek eklenen test sayısını ayrıca raporlar.

## 6. Her tranche için kapı

```bat
git rev-parse HEAD
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run -c Release --no-build --project src\KKL.WordStudio.UI\KKL.WordStudio.UI.csproj
```

- 0 warning;
- 0 error;
- beklenen tam test toplamı;
- ilgili UI smoke;
- exact head kaydı;
- Windows GREEN sonrası Ready for review ve squash merge.
