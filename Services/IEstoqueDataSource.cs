using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public interface IEstoqueDataSource
{
    List<Estoque> CriarLista(int quantidade, CancellationToken cancellationToken);

    IEnumerable<Estoque> Stream(int quantidade, CancellationToken cancellationToken);
}
