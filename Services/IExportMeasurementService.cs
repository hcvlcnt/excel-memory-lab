using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public interface IExportMeasurementService
{
    Task<ExportBenchmarkSummary> BenchmarkAsync(
        string scenario,
        int quantity,
        int repetitions,
        bool discardWarmUpRun,
        bool forceGc,
        CancellationToken cancellationToken);

    Task<ExportMeasurementResult> MeasureAsync(
        string scenario,
        int quantity,
        bool warmUp,
        bool forceGc,
        CancellationToken cancellationToken);
}
