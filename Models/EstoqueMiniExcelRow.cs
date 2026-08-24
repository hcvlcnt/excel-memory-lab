namespace OutOfMemoryWorkbook.Models;

public sealed class EstoqueMiniExcelRow
{
    public long Id { get; init; }

    public required string Codigo { get; init; }

    public required string Descricao { get; init; }

    public required string Status { get; init; }

    public int Quantidade { get; init; }

    public decimal CustoUnitario { get; init; }

    public decimal ValorEmEstoque { get; init; }

    public DateTime UltimaMovimentacao { get; init; }
}
