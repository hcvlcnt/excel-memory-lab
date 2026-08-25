namespace OutOfMemoryWorkbook.Models;

public sealed record ExportBenchmarkSummary(
    string Scenario,
    int Quantity,
    int Repetitions,
    bool WarmUpRunDiscarded,
    string MeasurementTarget,
    int SamplingIntervalMs,
    BenchmarkStatistics Statistics)
{
    public static ExportBenchmarkSummary From(
        IReadOnlyCollection<ExportMeasurementResult> runs,
        bool warmUpRunDiscarded)
    {
        var representativeRun = runs.Last();

        return new ExportBenchmarkSummary(
            Scenario: representativeRun.Scenario,
            Quantity: representativeRun.Quantity,
            Repetitions: runs.Count,
            WarmUpRunDiscarded: warmUpRunDiscarded,
            MeasurementTarget: representativeRun.MeasurementTarget,
            SamplingIntervalMs: representativeRun.SamplingIntervalMs,
            Statistics: BenchmarkStatistics.From(
                runs,
                run => run.AllocatedDuringMeasurementMiB,
                run => run.PeakManagedMemoryDeltaMiB,
                run => run.PeakWorkingSetDeltaMiB,
                run => run.PeakPrivateMemoryDeltaMiB,
                run => run.DurationMs,
                run => run.FileSizeMiB,
                run => run.Generation0Collections,
                run => run.Generation1Collections,
                run => run.Generation2Collections));
    }
}
