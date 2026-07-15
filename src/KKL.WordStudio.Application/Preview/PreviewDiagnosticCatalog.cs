namespace KKL.WordStudio.Application.Preview;

using KKL.WordStudio.Application.Tables;

public static class PreviewDiagnosticCodes
{
    public const string Unclassified = "UNCLASSIFIED";
    public const string SourceAccessError = "SOURCE_ACCESS_ERROR";
    public const string LayoutWarning = "LAYOUT_WARNING";
}

public sealed record PreviewDiagnosticDefinition(
    PreviewDiagnosticSeverity Severity,
    string Title);

/// <summary>
/// Single Application-owned catalog for stable diagnostic code, severity and
/// user-facing title. Neither Preview grouping nor UI code classifies messages.
/// </summary>
public static class PreviewDiagnosticCatalog
{
    public static PreviewDiagnosticDefinition Resolve(string code) => code switch
    {
        TableCompositionDiagnosticCodes.ConfigurationInvalid =>
            Warning("Seri/Adet düzeni yapılandırması geçersiz"),
        TableCompositionDiagnosticCodes.QuantityMissing =>
            Warning("Adet değeri eksik veya geçersiz"),
        TableCompositionDiagnosticCodes.QuantityInvalid =>
            Warning("Adet değeri eksik veya geçersiz"),
        TableCompositionDiagnosticCodes.QuantityConflicting =>
            Warning("Adet değerleri çelişkili"),
        TableCompositionDiagnosticCodes.MergeConflict =>
            Warning("Birleştirilecek satırlarda çelişki var"),
        TableCompositionDiagnosticCodes.SerialDuplicate =>
            Warning("Seri numarası tekrarı"),
        TableCompositionDiagnosticCodes.SerialQuantityMismatch =>
            Warning("Adet ile seri numarası sayısı uyuşmuyor"),
        TableCompositionDiagnosticCodes.LegacyWarning =>
            Warning("Tablo verisi uyarısı"),
        PreviewDiagnosticCodes.SourceAccessError =>
            new(PreviewDiagnosticSeverity.Error, "Kaynak veriye erişilemedi"),
        PreviewDiagnosticCodes.LayoutWarning =>
            Warning("Önizleme yerleşim uyarısı"),
        _ => Warning("Tanımlanamayan önizleme uyarısı")
    };

    private static PreviewDiagnosticDefinition Warning(string title) =>
        new(PreviewDiagnosticSeverity.Warning, title);
}
