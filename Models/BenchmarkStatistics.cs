namespace OutOfMemoryWorkbook.Models;

public sealed record BenchmarkStatistics(
    MetricStatistics AllocatedMemoryMiB,
    MetricStatistics SampledManagedPeakMiB,
    MetricStatistics SampledWorkingSetPeakMiB,
    MetricStatistics SampledPrivateMemoryPeakMiB,
    MetricStatistics DurationMs,
    MetricStatistics FileSizeMiB,
    MetricStatistics Generation0Collections,
    MetricStatistics Generation1Collections,
    MetricStatistics Generation2Collections)
{
    public static BenchmarkStatistics From<T>(
        IReadOnlyCollection<T> runs,
        Func<T, double> allocatedMemory,
        Func<T, double> sampledManagedPeak,
        Func<T, double> sampledWorkingSetPeak,
        Func<T, double> sampledPrivateMemoryPeak,
        Func<T, double> duration,
        Func<T, double> fileSize,
        Func<T, double> generation0Collections,
        Func<T, double> generation1Collections,
        Func<T, double> generation2Collections)
    {
        return new BenchmarkStatistics(
            AllocatedMemoryMiB: MetricStatistics.From(runs.Select(allocatedMemory)),
            SampledManagedPeakMiB: MetricStatistics.From(runs.Select(sampledManagedPeak)),
            SampledWorkingSetPeakMiB: MetricStatistics.From(runs.Select(sampledWorkingSetPeak)),
            SampledPrivateMemoryPeakMiB: MetricStatistics.From(runs.Select(sampledPrivateMemoryPeak)),
            DurationMs: MetricStatistics.From(runs.Select(duration)),
            FileSizeMiB: MetricStatistics.From(runs.Select(fileSize)),
            Generation0Collections: MetricStatistics.From(runs.Select(generation0Collections)),
            Generation1Collections: MetricStatistics.From(runs.Select(generation1Collections)),
            Generation2Collections: MetricStatistics.From(runs.Select(generation2Collections)));
    }
}
