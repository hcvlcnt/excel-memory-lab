namespace OutOfMemoryWorkbook.Models;

public sealed class EstoqueQueryBruto
{
    public long Id { get; init; }

    public required string Codigo { get; init; }

    public required string Descricao { get; init; }

    public StatusEstoque Status { get; init; }

    public int Quantidade { get; init; }

    public decimal CustoUnitario { get; init; }

    public DateTime UltimaMovimentacao { get; init; }
}
