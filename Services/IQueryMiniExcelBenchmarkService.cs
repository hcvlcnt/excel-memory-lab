using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public interface IQueryMiniExcelBenchmarkService
{
    IReadOnlyCollection<CenarioQueryMiniExcel> ObterCenarios();

    Task<DiagnosticoTraducaoQuery> DiagnosticarTraducaoAsync(
        CancellationToken cancellationToken);

    Task<ResultadoQueryMiniExcelBenchmark> MedirAsync(
        string cenario,
        int quantidade,
        bool aquecer,
        bool forcarGc,
        CancellationToken cancellationToken);
}
