using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public interface IInventoryDataSource
{
    List<InventoryItem> CreateList(int quantity, CancellationToken cancellationToken);

    IEnumerable<InventoryItem> Stream(int quantity, CancellationToken cancellationToken);
}
