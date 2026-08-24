using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class EstoqueDataSource : IEstoqueDataSource
{
    private static readonly DateTime DataBase = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public List<Estoque> CriarLista(int quantidade, CancellationToken cancellationToken)
    {
        var estoques = new List<Estoque>(quantidade);

        foreach (var estoque in Stream(quantidade, cancellationToken))
        {
            estoques.Add(estoque);
        }

        return estoques;
    }

    public IEnumerable<Estoque> Stream(int quantidade, CancellationToken cancellationToken)
    {
        for (var index = 1; index <= quantidade; index++)
        {
            if ((index & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            yield return new Estoque(
                Id: index,
                Codigo: $"SKU-{index:D9}",
                Descricao: $"Produto de demonstração {index:D9}",
                Categoria: $"Categoria {index % 25:D2}",
                Deposito: $"Depósito {index % 8:D2}",
                Quantidade: index % 10_000,
                CustoUnitario: decimal.Round(1.25m + (index % 100_000) * 0.013m, 2),
                UltimaMovimentacao: DataBase.AddMinutes(index));
        }
    }
}
