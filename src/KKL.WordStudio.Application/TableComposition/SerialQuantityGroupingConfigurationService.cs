namespace KKL.WordStudio.Application.TableComposition;

using KKL.WordStudio.Domain.Elements;
using KKL.WordStudio.Shared.Results;

public sealed class SerialQuantityGroupingConfigurationService : ISerialQuantityGroupingConfigurationService
{
    private readonly ISerialQuantityGroupingDetector detector;

    public SerialQuantityGroupingConfigurationService(ISerialQuantityGroupingDetector detector)
    {
        this.detector = detector;
    }

    public SerialQuantityGroupingDiagnosis Diagnose(TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var configuration = table.SerialQuantityGrouping;
        if (configuration is not null)
        {
            var match = table.Columns.FirstOrDefault(column => column.Id == configuration.MatchKeyColumnId);
            var serial = table.Columns.FirstOrDefault(column => column.Id == configuration.SerialNumberColumnId);
            var quantity = table.Columns.FirstOrDefault(column => column.Id == configuration.QuantityColumnId);
            var distinct = new HashSet<Guid>
            {
                configuration.MatchKeyColumnId,
                configuration.SerialNumberColumnId,
                configuration.QuantityColumnId
            }.Count == 3;

            if (match is not null && serial is not null && quantity is not null && distinct)
            {
                var mode = configuration.WasAutoDetected ? "Otomatik yapılandırıldı" : "Manuel yapılandırıldı";
                return new SerialQuantityGroupingDiagnosis
                {
                    IsConfigured = true,
                    IsAutoDetected = configuration.WasAutoDetected,
                    MatchKeyColumn = match,
                    SerialColumn = serial,
                    QuantityColumn = quantity,
                    StatusMessage = $"{mode}\nEşleşme: {DisplayName(match)}\nSeri No: {DisplayName(serial)}\nAdet: {DisplayName(quantity)}"
                };
            }

            return new SerialQuantityGroupingDiagnosis
            {
                IsConfigured = false,
                IsAutoDetected = configuration.WasAutoDetected,
                MatchKeyColumn = match,
                SerialColumn = serial,
                QuantityColumn = quantity,
                StatusMessage = "Yapılandırma geçersiz\nSeçili rol sütunlarından biri artık kullanılamıyor."
            };
        }

        return DiagnoseCandidates(table.Columns);
    }

    public Result ApplyManual(TableElement table, Guid matchKeyColumnId, Guid serialColumnId, Guid quantityColumnId)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (new HashSet<Guid> { matchKeyColumnId, serialColumnId, quantityColumnId }.Count != 3)
            return Result.Failure("Eşleşme, Seri No ve Adet alanları birbirinden farklı olmalıdır.");

        if (!table.Columns.Any(column => column.Id == matchKeyColumnId)
            || !table.Columns.Any(column => column.Id == serialColumnId)
            || !table.Columns.Any(column => column.Id == quantityColumnId))
        {
            return Result.Failure("Seçilen rol sütunlarından biri tabloda bulunamadı.");
        }

        table.SerialQuantityGrouping = new SerialQuantityGrouping
        {
            MatchKeyColumnId = matchKeyColumnId,
            SerialNumberColumnId = serialColumnId,
            QuantityColumnId = quantityColumnId,
            WasAutoDetected = false
        };
        return Result.Success();
    }

    public Result AutoDetect(TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);

        var detected = detector.Detect(table.Columns);
        if (detected is null)
            return Result.Failure(DiagnoseCandidates(table.Columns).StatusMessage);

        table.SerialQuantityGrouping = detected;
        return Result.Success();
    }

    public Result Remove(TableElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        table.SerialQuantityGrouping = null;
        return Result.Success();
    }

    private static SerialQuantityGroupingDiagnosis DiagnoseCandidates(IReadOnlyList<TableColumn> columns)
    {
        var matchKeys = columns.Where(ColumnRoleAliasNormalizer.MatchesMatchKey).ToList();
        var serials = columns.Where(ColumnRoleAliasNormalizer.MatchesSerial).ToList();
        var quantities = columns.Where(ColumnRoleAliasNormalizer.MatchesQuantity).ToList();

        string message;
        if (quantities.Count == 0)
            message = "Yapılandırılmadı\nAdet sütunu bulunamadı.";
        else if (quantities.Count > 1)
            message = "Yapılandırma belirsiz\nBirden fazla Adet alanı bulundu.";
        else if (serials.Count == 0)
            message = "Yapılandırılmadı\nSeri No sütunu bulunamadı.";
        else if (serials.Count > 1)
            message = "Yapılandırma belirsiz\nBirden fazla Seri No alanı bulundu.";
        else if (matchKeys.Count == 0)
            message = "Yapılandırılmadı\nEşleşme sütunu bulunamadı.";
        else if (matchKeys.Count > 1)
            message = "Yapılandırma belirsiz\nBirden fazla eşleşme alanı bulundu.";
        else if (new HashSet<Guid> { matchKeys[0].Id, serials[0].Id, quantities[0].Id }.Count != 3)
            message = "Yapılandırma belirsiz\nRol alanları birbirinden farklı olmalıdır.";
        else
            message = "Yapılandırılmadı\nOtomatik algılama için uygun alanlar bulundu.";

        return new SerialQuantityGroupingDiagnosis
        {
            IsConfigured = false,
            IsAutoDetected = false,
            MatchKeyColumn = matchKeys.Count == 1 ? matchKeys[0] : null,
            SerialColumn = serials.Count == 1 ? serials[0] : null,
            QuantityColumn = quantities.Count == 1 ? quantities[0] : null,
            StatusMessage = message
        };
    }

    private static string DisplayName(TableColumn column) =>
        !string.IsNullOrWhiteSpace(column.Header)
            ? column.Header
            : column.SourceField ?? "(adsız sütun)";
}
