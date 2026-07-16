# Sprint 25 — In-Application Usage Guide

## Scope

- Adds a full-workspace usage-guide page opened from the main command bar.
- Uses the approved `No / Tr İsim / Parça Numarası / NSN / Seri Numarası / Adet` fruit-demo workflow.
- Embeds ten real application screenshots as Base64 resources so self-contained single-file publish has no loose guide files.
- Keeps guide state UI-only; workbook, report, diagnostics and export state are not modified.

## Guide sections

1. Başlangıç Ekranı
2. Excel Kaynağı ve Worksheet
3. Veri Aralığını Düzenle
4. Word'e Aktar
5. İçindekiler ve Preview
6. Tablo Özellikleri
7. Başlık Özellikleri
8. Uyarılar
9. Hızlı Rapor
10. Word Dosyası Oluştur

## Editable content

- `Düzenleme Modu` edits the selected section without changing the live workspace.
- Title, purpose, step list, warning/tip text and the screenshot can be replaced.
- `Kaydet` persists only the selected section under `%LocalAppData%\KKL Word Studio\UsageGuide`.
- `Vazgeç` discards the current draft.
- `Seçili Bölümü Varsayılana Döndür` removes that section's local override.
- Custom images are copied into the user profile and loaded without keeping files locked.

## Image quality and asset safety

- Embedded and custom images use high-quality WPF scaling.
- `StretchDirection=DownOnly` prevents small screenshots from being enlarged into blurred full-width images.
- Embedded Base64 decoding tolerates whitespace and repository formatting artifacts.
- Architecture guards validate that every default screen decodes to a supported JPEG or PNG payload.

## Gate

Pending exact-head Windows Release build/test and manual guide navigation/editing smoke.
