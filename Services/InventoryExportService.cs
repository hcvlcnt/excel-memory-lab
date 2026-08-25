using NPOI.SS.UserModel;
using NPOI.XSSF.Streaming;
using NPOI.XSSF.UserModel;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class InventoryExportService(IInventoryDataSource dataSource) : IInventoryExportService
{
    private const int RowAccessWindowSize = 200;
    private const bool CompressTemporaryFiles = true;
    private const bool UseSharedStringsTable = false;

    public byte[] ExportCurrentScenario(
        int quantity,
        CancellationToken cancellationToken)
    {
        var inventoryItems = dataSource.CreateList(quantity, cancellationToken);
        var workbook = new XSSFWorkbook();

        try
        {
            PopulateWorkbook(workbook, inventoryItems, cancellationToken);

            using var memoryStream = new MemoryStream();
            workbook.Write(memoryStream, leaveOpen: true);

            // Intentionally reproduces the current scenario: ToArray creates a
            // second copy of the file in memory.
            return memoryStream.ToArray();
        }
        finally
        {
            workbook.Close();
        }
    }

    public Stream ExportXssfWithoutToArray(
        int quantity,
        CancellationToken cancellationToken)
    {
        var inventoryItems = dataSource.CreateList(quantity, cancellationToken);
        var workbook = new XSSFWorkbook();
        var memoryStream = new MemoryStream();

        try
        {
            PopulateWorkbook(workbook, inventoryItems, cancellationToken);
            workbook.Write(memoryStream, leaveOpen: true);
            memoryStream.Position = 0;

            return memoryStream;
        }
        catch
        {
            memoryStream.Dispose();
            throw;
        }
        finally
        {
            workbook.Close();
        }
    }

    public byte[] ExportSxssfWithList(
        int quantity,
        CancellationToken cancellationToken)
    {
        var inventoryItems = dataSource.CreateList(quantity, cancellationToken);
        var workbook = CreateSxssfWorkbook();

        try
        {
            PopulateWorkbook(workbook, inventoryItems, cancellationToken);

            using var memoryStream = new MemoryStream();
            workbook.Write(memoryStream, leaveOpen: true);
            return memoryStream.ToArray();
        }
        finally
        {
            workbook.Dispose();
        }
    }

    public string ExportSxssfToTemporaryFile(
        int quantity,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"estoque-{Guid.NewGuid():N}.xlsx");

        try
        {
            using var file = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);

            ExportSxssfToStream(quantity, file, cancellationToken);
            return path;
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    public void ExportSxssfToStream(
        int quantity,
        Stream target,
        CancellationToken cancellationToken)
    {
        var workbook = CreateSxssfWorkbook();

        try
        {
            var inventoryItems = dataSource.Stream(quantity, cancellationToken);
            PopulateWorkbook(workbook, inventoryItems, cancellationToken);
            workbook.Write(target, leaveOpen: true);
        }
        finally
        {
            workbook.Dispose();
        }
    }

    private static SXSSFWorkbook CreateSxssfWorkbook()
    {
        return new SXSSFWorkbook(
            workbook: null!,
            rowAccessWindowSize: RowAccessWindowSize,
            compressTmpFiles: CompressTemporaryFiles,
            useSharedStringsTable: UseSharedStringsTable);
    }

    private static void PopulateWorkbook(
        IWorkbook workbook,
        IEnumerable<InventoryItem> inventoryItems,
        CancellationToken cancellationToken)
    {
        var sheet = workbook.CreateSheet("Estoque");
        ConfigureColumns(sheet);

        var styles = CreateStyles(workbook);
        CreateHeader(sheet, styles.Header);

        var rowIndex = 1;

        foreach (var inventoryItem in inventoryItems)
        {
            if ((rowIndex & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var row = sheet.CreateRow(rowIndex++);

            row.CreateCell(0).SetCellValue((double)inventoryItem.Id);
            row.CreateCell(1).SetCellValue(inventoryItem.Code);
            row.CreateCell(2).SetCellValue(inventoryItem.Description);
            row.CreateCell(3).SetCellValue(inventoryItem.Category);
            row.CreateCell(4).SetCellValue(inventoryItem.Warehouse);
            row.CreateCell(5).SetCellValue(inventoryItem.Quantity);

            var costCell = row.CreateCell(6);
            costCell.SetCellValue((double)inventoryItem.UnitCost);
            costCell.CellStyle = styles.Currency;

            var dateCell = row.CreateCell(7);
            dateCell.SetCellValue(inventoryItem.LastMovement);
            dateCell.CellStyle = styles.DateTime;
        }
    }

    private static void CreateHeader(ISheet sheet, ICellStyle headerStyle)
    {
        string[] titles =
        [
            "Id",
            "Código",
            "Descrição",
            "Categoria",
            "Depósito",
            "Quantidade",
            "Custo unitário",
            "Última movimentação"
        ];

        var row = sheet.CreateRow(0);

        for (var index = 0; index < titles.Length; index++)
        {
            var cell = row.CreateCell(index);
            cell.SetCellValue(titles[index]);
            cell.CellStyle = headerStyle;
        }
    }

    private static WorkbookStyles CreateStyles(IWorkbook workbook)
    {
        var headerFont = workbook.CreateFont();
        headerFont.IsBold = true;
        headerFont.Color = IndexedColors.White.Index;

        var headerStyle = workbook.CreateCellStyle();
        headerStyle.SetFont(headerFont);
        headerStyle.FillForegroundColor = IndexedColors.DarkBlue.Index;
        headerStyle.FillPattern = FillPattern.SolidForeground;

        var dataFormat = workbook.CreateDataFormat();

        var moneyStyle = workbook.CreateCellStyle();
        moneyStyle.DataFormat = dataFormat.GetFormat("R$ #,##0.00");

        var dateTimeStyle = workbook.CreateCellStyle();
        dateTimeStyle.DataFormat = dataFormat.GetFormat("dd/MM/yyyy HH:mm:ss");

        return new WorkbookStyles(headerStyle, moneyStyle, dateTimeStyle);
    }

    private static void ConfigureColumns(ISheet sheet)
    {
        sheet.SetColumnWidth(0, 14 * 256);
        sheet.SetColumnWidth(1, 18 * 256);
        sheet.SetColumnWidth(2, 38 * 256);
        sheet.SetColumnWidth(3, 18 * 256);
        sheet.SetColumnWidth(4, 18 * 256);
        sheet.SetColumnWidth(5, 14 * 256);
        sheet.SetColumnWidth(6, 18 * 256);
        sheet.SetColumnWidth(7, 24 * 256);
    }

    private sealed record WorkbookStyles(
        ICellStyle Header,
        ICellStyle Currency,
        ICellStyle DateTime);
}
