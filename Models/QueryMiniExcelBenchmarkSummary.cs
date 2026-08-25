namespace OutOfMemoryWorkbook.Models;

public sealed record QueryMiniExcelBenchmarkSummary(
    string Scenario,
    int Quantity,
    int Repetitions,
    bool WarmUpRunDiscarded,
    string QueryStrategy,
    bool BuffersResults,
    bool ClientSideEnumConversion,
    string GeneratedSql,
    int SamplingIntervalMs,
    BenchmarkStatistics Statistics)
{
    public static QueryMiniExcelBenchmarkSummary From(
        IReadOnlyCollection<QueryMiniExcelBenchmarkResult> runs,
        bool warmUpRunDiscarded)
    {
        var representativeRun = runs.Last();

        return new QueryMiniExcelBenchmarkSummary(
            Scenario: representativeRun.Scenario,
            Quantity: representativeRun.Quantity,
            Repetitions: runs.Count,
            WarmUpRunDiscarded: warmUpRunDiscarded,
            QueryStrategy: representativeRun.QueryStrategy,
            BuffersResults: representativeRun.BuffersResults,
            ClientSideEnumConversion: representativeRun.ClientSideEnumConversion,
            GeneratedSql: representativeRun.GeneratedSql,
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
