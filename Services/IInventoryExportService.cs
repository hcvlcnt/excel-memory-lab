namespace OutOfMemoryWorkbook.Services;

public interface IInventoryExportService
{
    byte[] ExportCurrentScenario(int quantity, CancellationToken cancellationToken);

    Stream ExportXssfWithoutToArray(int quantity, CancellationToken cancellationToken);

    byte[] ExportSxssfWithList(int quantity, CancellationToken cancellationToken);

    string ExportSxssfToTemporaryFile(int quantity, CancellationToken cancellationToken);

    void ExportSxssfToStream(
        int quantity,
        Stream target,
        CancellationToken cancellationToken);
}
