namespace OutOfMemoryWorkbook.Models;

public sealed class RawInventoryQuery
{
    public long Id { get; init; }

    public required string Code { get; init; }

    public required string Description { get; init; }

    public InventoryStatus Status { get; init; }

    public int Quantity { get; init; }

    public decimal UnitCost { get; init; }

    public DateTime LastMovement { get; init; }
}
