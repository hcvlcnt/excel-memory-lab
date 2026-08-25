namespace OutOfMemoryWorkbook.Models;

public sealed class InventoryQueryBenchmark
{
    public long Id { get; set; }

    public required string Code { get; set; }

    public required string Description { get; set; }

    public InventoryStatus Status { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public DateTime LastMovement { get; set; }
}
