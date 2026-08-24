namespace OutOfMemoryWorkbook.Models;

public sealed class EstoqueQueryBenchmark
{
    public long Id { get; set; }

    public required string Codigo { get; set; }

    public required string Descricao { get; set; }

    public StatusEstoque Status { get; set; }

    public int Quantidade { get; set; }

    public decimal CustoUnitario { get; set; }

    public DateTime UltimaMovimentacao { get; set; }
}
