namespace KKL.WordStudio.Application.Preview;

/// <summary>
/// Stable user-facing diagnostic codes and severity rules. Classification is
/// intentionally kept in Application so Preview, the Control center and Word
/// readiness all speak the same language without introducing a second validator.
/// </summary>
public static class PreviewDiagnosticCodes
{
    public const string Unclassified = "UNCLASSIFIED";
    public const string SourceFileMissing = "SRC_FILE_MISSING";
    public const string SourceSheetMissing = "SRC_SHEET_MISSING";
    public const string SourceRangeInvalid = "SRC_RANGE_INVALID";
    public const string RequiredColumnMissing = "COLUMN_REQUIRED_MISSING";
    public const string SourceAccessError = "SRC_ACCESS_ERROR";
    public const string QuantityInvalid = "QUANTITY_INVALID";
    public const string SerialDuplicate = "SERIAL_DUPLICATE";
    public const string MergeConflict = "MERGE_CONFLICT";
    public const string RowsNotMerged = "ROWS_NOT_MERGED";
    public const string EmptyCells = "EMPTY_CELLS";
    public const string TableDataWarning = "TABLE_DATA_WARNING";
    public const string TableTooWide = "TABLE_TOO_WIDE";
    public const string TableSplit = "TABLE_SPLIT";
    public const string LayoutWarning = "LAYOUT_WARNING";
}

public sealed record PreviewDiagnosticRule(
    string Code,
    PreviewDiagnosticSeverity Severity,
    string Title,
    string UserMessage,
    string? AffectedColumn = null);

public static class PreviewDiagnosticCatalog
{
    public static PreviewDiagnosticRule ResolveComposition(string message)
    {
        var text = message ?? string.Empty;
        if (ContainsAny(text, "geçerli Adet değeri", "Adet değeri bulunamadı", "invalid quantity"))
            return new(PreviewDiagnosticCodes.QuantityInvalid, PreviewDiagnosticSeverity.Warning,
                "Adet değeri eksik veya geçersiz",
                "Adet alanı boş veya sayıya dönüştürülemiyor. Tablo oluşturulur ancak etkilenen hücreler kontrol edilmelidir.",
                "Adet");

        if (ContainsAny(text, "çelişkili değerler", "conflicting values"))
            return new(PreviewDiagnosticCodes.MergeConflict, PreviewDiagnosticSeverity.Warning,
                "Birleştirilecek satırlarda çelişki var",
                "Aynı anahtara ait satırlarda farklı değerler bulundu. Veri kaybını önlemek için satırlar ayrı bırakıldı.");

        if (ContainsAny(text, "yinelenen seri", "seri numarası tekrarı", "duplicate serial"))
            return new(PreviewDiagnosticCodes.SerialDuplicate, PreviewDiagnosticSeverity.Warning,
                "Seri numarası tekrarı",
                "Aynı seri numarası birden fazla satırda bulundu. Word çıktısı üretilebilir ancak tekrarlar kontrol edilmelidir.",
                "Seri Numarası");

        if (ContainsAny(text, "birleştirilmedi", "not merged"))
            return new(PreviewDiagnosticCodes.RowsNotMerged, PreviewDiagnosticSeverity.Warning,
                "Satırlar güvenli biçimde birleştirilemedi",
                "Birleştirme koşulları sağlanmadığı için ilgili satırlar ayrı tutuldu.");

        if (ContainsAny(text, "boş hücre", "empty cell"))
            return new(PreviewDiagnosticCodes.EmptyCells, PreviewDiagnosticSeverity.Warning,
                "Boş hücreler bulundu",
                "Bazı hücreler boş. Tablo oluşturulur ancak eksik alanlar kontrol edilmelidir.");

        return new(PreviewDiagnosticCodes.TableDataWarning, PreviewDiagnosticSeverity.Warning,
            "Tablo verisi kontrol edilmeli",
            "Tablo verisinde kullanıcı kontrolü gerektiren bir durum bulundu.");
    }

    public static PreviewDiagnosticRule ResolveSourceError(string message)
    {
        var text = message ?? string.Empty;
        if (ContainsAny(text, "file not found", "dosya bulunamadı", "cannot find file"))
            return new(PreviewDiagnosticCodes.SourceFileMissing, PreviewDiagnosticSeverity.Error,
                "Excel dosyası bulunamadı",
                "Bağlı Excel dosyasına erişilemiyor. Kaynak yeniden seçilmeden Word çıktısı oluşturulamaz.");

        if (ContainsAny(text, "worksheet", "sheet", "çalışma sayfası", "sayfa bulunamadı"))
            return new(PreviewDiagnosticCodes.SourceSheetMissing, PreviewDiagnosticSeverity.Error,
                "Çalışma sayfası bulunamadı",
                "Bağlı Excel çalışma sayfası artık mevcut değil. Kaynak düzeltilmeden Word çıktısı oluşturulamaz.");

        if (ContainsAny(text, "range", "aralık"))
            return new(PreviewDiagnosticCodes.SourceRangeInvalid, PreviewDiagnosticSeverity.Error,
                "Veri aralığı okunamıyor",
                "Tablonun bağlı veri aralığı geçersiz veya okunamıyor.");

        if (ContainsAny(text, "column", "sütun"))
            return new(PreviewDiagnosticCodes.RequiredColumnMissing, PreviewDiagnosticSeverity.Error,
                "Zorunlu sütun bulunamadı",
                "Tablonun ihtiyaç duyduğu kaynak sütun bulunamıyor.");

        return new(PreviewDiagnosticCodes.SourceAccessError, PreviewDiagnosticSeverity.Error,
            "Kaynak veriye erişilemedi",
            "Tablonun Excel kaynağı çözümlenemedi. Sorun düzeltilmeden Word çıktısı oluşturulamaz.");
    }

    public static PreviewDiagnosticRule ResolveLayout(string message)
    {
        var text = message ?? string.Empty;
        if (ContainsAny(text, "too wide", "çok geniş", "sayfaya sığm", "width"))
            return new(PreviewDiagnosticCodes.TableTooWide, PreviewDiagnosticSeverity.Warning,
                "Tablo sayfaya geniş geliyor",
                "Tablo Word sayfasına sığdırılacak; okunabilirliği kontrol edin.");

        if (ContainsAny(text, "table split", "tablo bölünd", "fragment"))
            return new(PreviewDiagnosticCodes.TableSplit, PreviewDiagnosticSeverity.Information,
                "Tablo birden fazla sayfaya bölündü",
                "Tablo uzun olduğu için Word sayfaları arasında otomatik olarak bölündü.");

        return new(PreviewDiagnosticCodes.LayoutWarning, PreviewDiagnosticSeverity.Warning,
            "Önizleme yerleşimi kontrol edilmeli",
            "Sayfa yerleşiminde kullanıcı kontrolü gerektiren bir durum bulundu.");
    }

    public static string BuildGroupMessage(
        string code,
        int occurrenceCount,
        string? affectedColumn,
        string fallback)
    {
        var countText = occurrenceCount == 1 ? "1 kayıt" : $"{occurrenceCount} kayıt";
        return code switch
        {
            PreviewDiagnosticCodes.QuantityInvalid => $"{countText} içinde Adet alanı boş veya sayıya dönüştürülemiyor.",
            PreviewDiagnosticCodes.SerialDuplicate => $"{countText} içinde seri numarası tekrarı bulundu.",
            PreviewDiagnosticCodes.MergeConflict => $"{countText} içinde birleştirme değerleri çelişiyor; satırlar ayrı bırakıldı.",
            PreviewDiagnosticCodes.RowsNotMerged => $"{countText} güvenli biçimde birleştirilemedi ve ayrı tutuldu.",
            PreviewDiagnosticCodes.EmptyCells => $"{countText} içinde boş hücre bulundu.",
            PreviewDiagnosticCodes.TableTooWide => "Tablo Word sayfasına geniş geliyor; çıktıdaki okunabilirliği kontrol edin.",
            PreviewDiagnosticCodes.TableSplit => "Tablo uzun olduğu için birden fazla Word sayfasına bölündü.",
            _ when !string.IsNullOrWhiteSpace(affectedColumn) => $"{affectedColumn} alanında {countText} kontrol edilmeli.",
            _ => fallback
        };
    }

    private static bool ContainsAny(string source, params string[] values) =>
        values.Any(value => source.Contains(value, StringComparison.OrdinalIgnoreCase));
}
