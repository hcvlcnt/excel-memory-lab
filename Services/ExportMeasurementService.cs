using System.Diagnostics;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class ExportMeasurementService(
    IInventoryExportService exportService) : IExportMeasurementService
{
    private const int SamplingIntervalMs = 10;
    private const int WarmUpQuantity = 100;
    private readonly SemaphoreSlim _measurementLock = new(1, 1);

    public async Task<ExportBenchmarkSummary> BenchmarkAsync(
        string scenario,
        int quantity,
        int repetitions,
        bool discardWarmUpRun,
        bool forceGc,
        CancellationToken cancellationToken)
    {
        if (repetitions is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(repetitions),
                repetitions,
                "A quantidade de repetições deve estar entre 1 e 10.");
        }

        await _measurementLock.WaitAsync(cancellationToken);

        try
        {
            if (discardWarmUpRun)
            {
                await MeasureCoreAsync(
                    scenario,
                    quantity,
                    warmUp: false,
                    forceGc,
                    cancellationToken);
            }

            var runs = new List<ExportMeasurementResult>(repetitions);

            for (var repetition = 0; repetition < repetitions; repetition++)
            {
                runs.Add(await MeasureCoreAsync(
                    scenario,
                    quantity,
                    warmUp: false,
                    forceGc,
                    cancellationToken));
            }

            return ExportBenchmarkSummary.From(runs, discardWarmUpRun);
        }
        finally
        {
            _measurementLock.Release();
        }
    }

    public async Task<ExportMeasurementResult> MeasureAsync(
        string scenario,
        int quantity,
        bool warmUp,
        bool forceGc,
        CancellationToken cancellationToken)
    {
        await _measurementLock.WaitAsync(cancellationToken);

        try
        {
            return await MeasureCoreAsync(
                scenario,
                quantity,
                warmUp,
                forceGc,
                cancellationToken);
        }
        finally
        {
            _measurementLock.Release();
        }
    }

    private async Task<ExportMeasurementResult> MeasureCoreAsync(
        string scenario,
        int quantity,
        bool warmUp,
        bool forceGc,
        CancellationToken cancellationToken)
    {
        if (!ExportScenarios.All.Contains(scenario))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scenario),
                scenario,
                "Cenário de exportação desconhecido.");
        }

        MeasurementArtifact? artifact = null;

        try
        {
            if (warmUp)
            {
                using var warmUpArtifact = ExecuteScenario(
                    scenario,
                    Math.Min(quantity, WarmUpQuantity),
                    cancellationToken);
            }

            if (forceGc)
            {
                ForceFullCollection();
            }

            var initialSnapshot = MemorySnapshot.Capture();
            var peaks = new MemoryPeaks(initialSnapshot);
            var initialAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true);
            var initialGeneration0Collections = GC.CollectionCount(0);
            var initialGeneration1Collections = GC.CollectionCount(1);
            var initialGeneration2Collections = GC.CollectionCount(2);

            using var samplingCancellation = new CancellationTokenSource();
            var samplingTask = SampleAsync(peaks, samplingCancellation.Token);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                artifact = await Task.Run(
                    () => ExecuteScenario(scenario, quantity, cancellationToken),
                    cancellationToken);

                stopwatch.Stop();
                peaks.Record(MemorySnapshot.Capture());
            }
            finally
            {
                stopwatch.Stop();
                samplingCancellation.Cancel();

                try
                {
                    await samplingTask;
                }
                catch (OperationCanceledException)
                {
                    // Encerramento esperado da amostragem.
                }
            }

            var allocatedBytes = Math.Max(
                0,
                GC.GetTotalAllocatedBytes(precise: true) - initialAllocatedBytes);

            var result = new ExportMeasurementResult(
                Scenario: scenario,
                Quantity: quantity,
                MeasurementTarget: artifact.Target,
                WarmUpExecuted: warmUp,
                GcForced: forceGc,
                SamplingIntervalMs: SamplingIntervalMs,
                SampleCount: peaks.SampleCount,
                DurationMs: Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                FileSizeBytes: artifact.SizeBytes,
                InitialManagedMemoryBytes: initialSnapshot.ManagedMemoryBytes,
                PeakManagedMemoryBytes: peaks.PeakManagedMemoryBytes,
                PeakManagedMemoryDeltaBytes: CalculateDelta(
                    peaks.PeakManagedMemoryBytes,
                    initialSnapshot.ManagedMemoryBytes),
                InitialWorkingSetBytes: initialSnapshot.WorkingSetBytes,
                PeakWorkingSetBytes: peaks.PeakWorkingSetBytes,
                PeakWorkingSetDeltaBytes: CalculateDelta(
                    peaks.PeakWorkingSetBytes,
                    initialSnapshot.WorkingSetBytes),
                InitialPrivateMemoryBytes: initialSnapshot.PrivateMemoryBytes,
                PeakPrivateMemoryBytes: peaks.PeakPrivateMemoryBytes,
                PeakPrivateMemoryDeltaBytes: CalculateDelta(
                    peaks.PeakPrivateMemoryBytes,
                    initialSnapshot.PrivateMemoryBytes),
                BytesAllocatedDuringMeasurement: allocatedBytes,
                Generation0Collections: GC.CollectionCount(0) - initialGeneration0Collections,
                Generation1Collections: GC.CollectionCount(1) - initialGeneration1Collections,
                Generation2Collections: GC.CollectionCount(2) - initialGeneration2Collections);

            GC.KeepAlive(artifact);
            return result;
        }
        finally
        {
            artifact?.Dispose();
        }
    }

    private MeasurementArtifact ExecuteScenario(
        string scenario,
        int quantity,
        CancellationToken cancellationToken)
    {
        return scenario.ToLowerInvariant() switch
        {
            ExportScenarios.Current => ExecuteCurrent(quantity, cancellationToken),
            ExportScenarios.XssfWithoutToArray => ExecuteXssfWithoutToArray(
                quantity,
                cancellationToken),
            ExportScenarios.SxssfWithList => ExecuteSxssfWithList(
                quantity,
                cancellationToken),
            ExportScenarios.SxssfFileStream => ExecuteSxssfFileStream(
                quantity,
                cancellationToken),
            ExportScenarios.SxssfResponseStream => ExecuteSxssfResponseStream(
                quantity,
                cancellationToken),
            _ => throw new InvalidOperationException("Cenário não suportado.")
        };
    }

    private MeasurementArtifact ExecuteCurrent(
        int quantity,
        CancellationToken cancellationToken)
    {
        var content = exportService.ExportCurrentScenario(quantity, cancellationToken);
        return MeasurementArtifact.FromArray(content, "byte[] retornado por MemoryStream.ToArray()");
    }

    private MeasurementArtifact ExecuteXssfWithoutToArray(
        int quantity,
        CancellationToken cancellationToken)
    {
        var stream = exportService.ExportXssfWithoutToArray(quantity, cancellationToken);
        return MeasurementArtifact.FromStream(stream, "MemoryStream sem ToArray()");
    }

    private MeasurementArtifact ExecuteSxssfWithList(
        int quantity,
        CancellationToken cancellationToken)
    {
        var content = exportService.ExportSxssfWithList(quantity, cancellationToken);
        return MeasurementArtifact.FromArray(content, "byte[] com SXSSFWorkbook e lista");
    }

    private MeasurementArtifact ExecuteSxssfFileStream(
        int quantity,
        CancellationToken cancellationToken)
    {
        var path = exportService.ExportSxssfToTemporaryFile(
            quantity,
            cancellationToken);

        return MeasurementArtifact.FromTemporaryFile(path);
    }

    private MeasurementArtifact ExecuteSxssfResponseStream(
        int quantity,
        CancellationToken cancellationToken)
    {
        var stream = new CountingWriteStream();
        exportService.ExportSxssfToStream(quantity, stream, cancellationToken);

        return MeasurementArtifact.FromStream(
            stream,
            "CountingWriteStream sem buffer, simulando Response.Body");
    }

    private static async Task SampleAsync(
        MemoryPeaks peaks,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            peaks.Record(MemorySnapshot.Capture());
            await Task.Delay(SamplingIntervalMs, cancellationToken);
        }
    }

    private static void ForceFullCollection()
    {
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Aggressive,
            blocking: true,
            compacting: true);

        GC.WaitForPendingFinalizers();

        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Aggressive,
            blocking: true,
            compacting: true);
    }

    private static long CalculateDelta(long peak, long initial)
    {
        return Math.Max(0, peak - initial);
    }

    private sealed record MemorySnapshot(
        long ManagedMemoryBytes,
        long WorkingSetBytes,
        long PrivateMemoryBytes)
    {
        public static MemorySnapshot Capture()
        {
            using var process = Process.GetCurrentProcess();

            return new MemorySnapshot(
                ManagedMemoryBytes: GC.GetTotalMemory(forceFullCollection: false),
                WorkingSetBytes: process.WorkingSet64,
                PrivateMemoryBytes: process.PrivateMemorySize64);
        }
    }

    private sealed class MemoryPeaks(MemorySnapshot initial)
    {
        private long _peakManagedMemoryBytes = initial.ManagedMemoryBytes;
        private long _peakWorkingSetBytes = initial.WorkingSetBytes;
        private long _peakPrivateMemoryBytes = initial.PrivateMemoryBytes;
        private int _sampleCount = 1;

        public long PeakManagedMemoryBytes => Volatile.Read(
            ref _peakManagedMemoryBytes);

        public long PeakWorkingSetBytes => Volatile.Read(ref _peakWorkingSetBytes);

        public long PeakPrivateMemoryBytes => Volatile.Read(ref _peakPrivateMemoryBytes);

        public int SampleCount => Volatile.Read(ref _sampleCount);

        public void Record(MemorySnapshot snapshot)
        {
            UpdateMaximum(
                ref _peakManagedMemoryBytes,
                snapshot.ManagedMemoryBytes);

            UpdateMaximum(ref _peakWorkingSetBytes, snapshot.WorkingSetBytes);
            UpdateMaximum(ref _peakPrivateMemoryBytes, snapshot.PrivateMemoryBytes);
            Interlocked.Increment(ref _sampleCount);
        }

        private static void UpdateMaximum(ref long target, long candidate)
        {
            var current = Volatile.Read(ref target);

            while (candidate > current)
            {
                var previous = Interlocked.CompareExchange(ref target, candidate, current);

                if (previous == current)
                {
                    return;
                }

                current = previous;
            }
        }
    }

    private sealed class MeasurementArtifact : IDisposable
    {
        private object? _retainedReference;
        private IDisposable? _disposableResource;
        private string? _temporaryPath;

        private MeasurementArtifact(
            long sizeBytes,
            string target,
            object? retainedReference = null,
            IDisposable? disposableResource = null,
            string? temporaryPath = null)
        {
            SizeBytes = sizeBytes;
            Target = target;
            _retainedReference = retainedReference;
            _disposableResource = disposableResource;
            _temporaryPath = temporaryPath;
        }

        public long SizeBytes { get; }

        public string Target { get; }

        public static MeasurementArtifact FromArray(byte[] content, string target)
        {
            return new MeasurementArtifact(
                content.LongLength,
                target,
                retainedReference: content);
        }

        public static MeasurementArtifact FromStream(Stream stream, string target)
        {
            return new MeasurementArtifact(
                stream.Length,
                target,
                disposableResource: stream);
        }

        public static MeasurementArtifact FromTemporaryFile(string path)
        {
            return new MeasurementArtifact(
                new FileInfo(path).Length,
                "arquivo temporário em disco",
                temporaryPath: path);
        }

        public void Dispose()
        {
            _disposableResource?.Dispose();

            if (_temporaryPath is not null)
            {
                File.Delete(_temporaryPath);
            }

            _retainedReference = null;
            _disposableResource = null;
            _temporaryPath = null;
        }
    }

    private sealed class CountingWriteStream : Stream
    {
        private long _length;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => Interlocked.Read(ref _length);

        public override long Position
        {
            get => Length;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Interlocked.Add(ref _length, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Interlocked.Add(ref _length, buffer.Length);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Add(ref _length, buffer.Length);
            return ValueTask.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
