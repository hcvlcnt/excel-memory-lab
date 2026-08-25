namespace OutOfMemoryWorkbook.Models;

public sealed record QueryMiniExcelBenchmarkResult(
    string Scenario,
    int Quantity,
    string QueryStrategy,
    bool BuffersResults,
    bool ClientSideEnumConversion,
    string GeneratedSql,
    string TemporaryFile,
    int SamplingIntervalMs,
    int SampleCount,
    double DurationMs,
    long FileSizeBytes,
    long PeakManagedMemoryDeltaBytes,
    long PeakWorkingSetDeltaBytes,
    long PeakPrivateMemoryDeltaBytes,
    long BytesAllocatedDuringMeasurement,
    int Generation0Collections,
    int Generation1Collections,
    int Generation2Collections)
{
    public double FileSizeMiB => ConvertToMiB(FileSizeBytes);

    public double PeakManagedMemoryDeltaMiB => ConvertToMiB(
        PeakManagedMemoryDeltaBytes);

    public double PeakWorkingSetDeltaMiB => ConvertToMiB(PeakWorkingSetDeltaBytes);

    public double PeakPrivateMemoryDeltaMiB => ConvertToMiB(PeakPrivateMemoryDeltaBytes);

    public double AllocatedDuringMeasurementMiB => ConvertToMiB(BytesAllocatedDuringMeasurement);

    private static double ConvertToMiB(long bytes)
    {
        return Math.Round(bytes / 1024d / 1024d, 2);
    }
}
