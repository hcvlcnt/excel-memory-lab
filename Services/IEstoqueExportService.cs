namespace OutOfMemoryWorkbook.Services;

public interface IEstoqueExportService
{
    byte[] ExportarCenarioAtual(int quantidade, CancellationToken cancellationToken);

    Stream ExportarXssfSemToArray(int quantidade, CancellationToken cancellationToken);

    byte[] ExportarSxssfComLista(int quantidade, CancellationToken cancellationToken);

    string ExportarSxssfParaArquivoTemporario(int quantidade, CancellationToken cancellationToken);

    void ExportarSxssfParaStream(
        int quantidade,
        Stream destino,
        CancellationToken cancellationToken);
}
