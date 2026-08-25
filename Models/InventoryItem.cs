namespace OutOfMemoryWorkbook.Models;

public sealed record InventoryItem(
    long Id,
    string Code,
    string Description,
    string Category,
    string Warehouse,
    int Quantity,
    decimal UnitCost,
    DateTime LastMovement);
