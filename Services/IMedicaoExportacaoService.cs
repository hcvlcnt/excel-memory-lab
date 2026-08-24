using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public interface IMedicaoExportacaoService
{
    Task<ResultadoMedicaoExportacao> MedirAsync(
        string cenario,
        int quantidade,
        bool aquecer,
        bool forcarGc,
        CancellationToken cancellationToken);
}
