using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class InventoryDataSource : IInventoryDataSource
{
    private static readonly DateTime BaseDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public List<InventoryItem> CreateList(int quantity, CancellationToken cancellationToken)
    {
        var inventoryItems = new List<InventoryItem>(quantity);

        foreach (var inventoryItem in Stream(quantity, cancellationToken))
        {
            inventoryItems.Add(inventoryItem);
        }

        return inventoryItems;
    }

    public IEnumerable<InventoryItem> Stream(int quantity, CancellationToken cancellationToken)
    {
        for (var index = 1; index <= quantity; index++)
        {
            if ((index & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            yield return new InventoryItem(
                Id: index,
                Code: $"SKU-{index:D9}",
                Description: $"Produto de demonstração {index:D9}",
                Category: $"Categoria {index % 25:D2}",
                Warehouse: $"Depósito {index % 8:D2}",
                Quantity: index % 10_000,
                UnitCost: decimal.Round(1.25m + (index % 100_000) * 0.013m, 2),
                LastMovement: BaseDate.AddMinutes(index));
        }
    }
}
