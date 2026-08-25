using MiniExcelLibs.Attributes;

namespace OutOfMemoryWorkbook.Models;

public sealed class InventoryMiniExcelRow
{
    [ExcelColumnName("ID")]
    public long Id { get; init; }

    [ExcelColumnName("Código")]
    public required string Code { get; init; }

    [ExcelColumnName("Descrição")]
    public required string Description { get; init; }

    [ExcelColumnName("Status")]
    public required string Status { get; init; }

    [ExcelColumnName("Quantidade")]
    public int Quantity { get; init; }

    [ExcelColumnName("Custo unitário")]
    public decimal UnitCost { get; init; }

    [ExcelColumnName("Valor em estoque")]
    public decimal InventoryValue { get; init; }

    [ExcelColumnName("Última movimentação")]
    public DateTime LastMovement { get; init; }
}
