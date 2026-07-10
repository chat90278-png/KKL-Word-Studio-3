namespace KKL.WordStudio.Application.TableComposition;

using KKL.WordStudio.Domain.Elements;

public sealed class SerialQuantityGroupingDiagnosis
{
    public required bool IsConfigured { get; init; }
    public required bool IsAutoDetected { get; init; }
    public TableColumn? MatchKeyColumn { get; init; }
    public TableColumn? SerialColumn { get; init; }
    public TableColumn? QuantityColumn { get; init; }
    public required string StatusMessage { get; init; }
}
