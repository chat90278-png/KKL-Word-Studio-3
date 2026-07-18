# Sprint 25 — In-Application Usage Guide

## Scope

- Adds a full-workspace usage-guide page opened from the main command bar.
- Uses general product workflow examples that do not require a specific worksheet name, column schema or demo workbook.
- Embeds ten real application screenshots as direct JPEG resources so self-contained single-file publish has no loose guide files.
- Keeps guide state UI-only; workbook, report, diagnostics and export state are not modified.

## Guide sections

1. Başlangıç Ekranı
2. Excel Kaynağı ve Sayfa Seçimi
3. Sütun Ekleme ve Silme
4. Veri Aralığını Düzenle
5. Word'e Aktar
6. İçindekiler ve Önizleme
7. Tablo Özellikleri
8. Başlık Özellikleri
9. Uyarılar
10. Hızlı Rapor
11. Word Dosyası Oluştur

## Editable content

- `Düzenleme Modu` edits the selected section without changing the live workspace.
- Title, purpose, step list, warning/tip text and the screenshot can be replaced.
- `Kaydet` persists only the selected section under `%LocalAppData%\KKL Word Studio\UsageGuide`.
- `Vazgeç` discards the current draft.
- `Seçili Bölümü Varsayılana Döndür` removes that section's local override.
- Custom images are copied into the user profile and loaded without keeping files locked.

## Image quality and asset safety

- Built-in screenshots are stored under `Assets/GuideScreens` as real `.jpg` files.
- Embedded and custom images use high-quality WPF scaling.
- `StretchDirection=DownOnly` prevents small screenshots from being enlarged into blurred full-width images.
- Embedded JPEG resources are read directly as binary streams; no Base64 decoding layer remains.
- Architecture guards validate that every default screen exists and has a JPEG signature.

## Gate

Pending exact-head Windows Release build/test and manual guide navigation/editing smoke.
