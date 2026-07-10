namespace KKL.WordStudio.Domain.Elements;

/// <summary>
/// Persisted stable TableColumn.Id role identities for serial/quantity grouped
/// table composition. Display headers and raw column indexes are deliberately
/// not part of this contract.
/// </summary>
public sealed class SerialQuantityGrouping
{
    public Guid MatchKeyColumnId { get; set; }
    public Guid SerialNumberColumnId { get; set; }
    public Guid QuantityColumnId { get; set; }
    public bool WasAutoDetected { get; set; }
}
