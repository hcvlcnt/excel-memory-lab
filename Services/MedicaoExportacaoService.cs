using System.Diagnostics;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class MedicaoExportacaoService(
    IEstoqueExportService exportService) : IMedicaoExportacaoService
{
    private const int IntervaloAmostragemMs = 10;
    private const int QuantidadeAquecimento = 100;
    private readonly SemaphoreSlim _measurementLock = new(1, 1);

    public async Task<ResultadoMedicaoExportacao> MedirAsync(
        string cenario,
        int quantidade,
        bool aquecer,
        bool forcarGc,
        CancellationToken cancellationToken)
    {
        if (!CenariosExportacao.Todos.Contains(cenario))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cenario),
                cenario,
                "Cenário de exportação desconhecido.");
        }

        await _measurementLock.WaitAsync(cancellationToken);

        ArtefatoMedicao? artefato = null;

        try
        {
            if (aquecer)
            {
                using var artefatoAquecimento = ExecutarCenario(
                    cenario,
                    Math.Min(quantidade, QuantidadeAquecimento),
                    cancellationToken);
            }

            if (forcarGc)
            {
                ForcarColetaCompleta();
            }

            var snapshotInicial = SnapshotMemoria.Capturar();
            var picos = new PicosMemoria(snapshotInicial);
            var bytesAlocadosInicial = GC.GetTotalAllocatedBytes(precise: true);
            var coletasGeracao0Inicial = GC.CollectionCount(0);
            var coletasGeracao1Inicial = GC.CollectionCount(1);
            var coletasGeracao2Inicial = GC.CollectionCount(2);

            using var amostragemCancellation = new CancellationTokenSource();
            var amostragemTask = AmostrarAsync(picos, amostragemCancellation.Token);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                artefato = await Task.Run(
                    () => ExecutarCenario(cenario, quantidade, cancellationToken),
                    cancellationToken);

                stopwatch.Stop();
                picos.Registrar(SnapshotMemoria.Capturar());
            }
            finally
            {
                stopwatch.Stop();
                amostragemCancellation.Cancel();

                try
                {
                    await amostragemTask;
                }
                catch (OperationCanceledException)
                {
                    // Encerramento esperado da amostragem.
                }
            }

            var bytesAlocados = Math.Max(
                0,
                GC.GetTotalAllocatedBytes(precise: true) - bytesAlocadosInicial);

            var resultado = new ResultadoMedicaoExportacao(
                Cenario: cenario,
                Quantidade: quantidade,
                DestinoMedicao: artefato.Destino,
                AquecimentoExecutado: aquecer,
                GcForcado: forcarGc,
                IntervaloAmostragemMs: IntervaloAmostragemMs,
                QuantidadeAmostras: picos.QuantidadeAmostras,
                DuracaoMs: Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                TamanhoArquivoBytes: artefato.TamanhoBytes,
                MemoriaGerenciadaInicialBytes: snapshotInicial.MemoriaGerenciadaBytes,
                PicoMemoriaGerenciadaBytes: picos.PicoMemoriaGerenciadaBytes,
                DeltaPicoMemoriaGerenciadaBytes: CalcularDelta(
                    picos.PicoMemoriaGerenciadaBytes,
                    snapshotInicial.MemoriaGerenciadaBytes),
                WorkingSetInicialBytes: snapshotInicial.WorkingSetBytes,
                PicoWorkingSetBytes: picos.PicoWorkingSetBytes,
                DeltaPicoWorkingSetBytes: CalcularDelta(
                    picos.PicoWorkingSetBytes,
                    snapshotInicial.WorkingSetBytes),
                MemoriaPrivadaInicialBytes: snapshotInicial.MemoriaPrivadaBytes,
                PicoMemoriaPrivadaBytes: picos.PicoMemoriaPrivadaBytes,
                DeltaPicoMemoriaPrivadaBytes: CalcularDelta(
                    picos.PicoMemoriaPrivadaBytes,
                    snapshotInicial.MemoriaPrivadaBytes),
                BytesAlocadosDuranteMedicao: bytesAlocados,
                ColetasGeracao0: GC.CollectionCount(0) - coletasGeracao0Inicial,
                ColetasGeracao1: GC.CollectionCount(1) - coletasGeracao1Inicial,
                ColetasGeracao2: GC.CollectionCount(2) - coletasGeracao2Inicial);

            GC.KeepAlive(artefato);
            return resultado;
        }
        finally
        {
            artefato?.Dispose();
            _measurementLock.Release();
        }
    }

    private ArtefatoMedicao ExecutarCenario(
        string cenario,
        int quantidade,
        CancellationToken cancellationToken)
    {
        return cenario.ToLowerInvariant() switch
        {
            CenariosExportacao.Atual => ExecutarAtual(quantidade, cancellationToken),
            CenariosExportacao.XssfSemToArray => ExecutarXssfSemToArray(
                quantidade,
                cancellationToken),
            CenariosExportacao.SxssfComLista => ExecutarSxssfComLista(
                quantidade,
                cancellationToken),
            CenariosExportacao.SxssfStreamArquivo => ExecutarSxssfStreamArquivo(
                quantidade,
                cancellationToken),
            CenariosExportacao.SxssfStreamResponse => ExecutarSxssfStreamResponse(
                quantidade,
                cancellationToken),
            _ => throw new InvalidOperationException("Cenário não suportado.")
        };
    }

    private ArtefatoMedicao ExecutarAtual(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var conteudo = exportService.ExportarCenarioAtual(quantidade, cancellationToken);
        return ArtefatoMedicao.ParaArray(conteudo, "byte[] retornado por MemoryStream.ToArray()");
    }

    private ArtefatoMedicao ExecutarXssfSemToArray(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var stream = exportService.ExportarXssfSemToArray(quantidade, cancellationToken);
        return ArtefatoMedicao.ParaStream(stream, "MemoryStream sem ToArray()");
    }

    private ArtefatoMedicao ExecutarSxssfComLista(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var conteudo = exportService.ExportarSxssfComLista(quantidade, cancellationToken);
        return ArtefatoMedicao.ParaArray(conteudo, "byte[] com SXSSFWorkbook e lista");
    }

    private ArtefatoMedicao ExecutarSxssfStreamArquivo(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var caminho = exportService.ExportarSxssfParaArquivoTemporario(
            quantidade,
            cancellationToken);

        return ArtefatoMedicao.ParaArquivoTemporario(caminho);
    }

    private ArtefatoMedicao ExecutarSxssfStreamResponse(
        int quantidade,
        CancellationToken cancellationToken)
    {
        var stream = new CountingWriteStream();
        exportService.ExportarSxssfParaStream(quantidade, stream, cancellationToken);

        return ArtefatoMedicao.ParaStream(
            stream,
            "CountingWriteStream sem buffer, simulando Response.Body");
    }

    private static async Task AmostrarAsync(
        PicosMemoria picos,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            picos.Registrar(SnapshotMemoria.Capturar());
            await Task.Delay(IntervaloAmostragemMs, cancellationToken);
        }
    }

    private static void ForcarColetaCompleta()
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

    private static long CalcularDelta(long pico, long inicial)
    {
        return Math.Max(0, pico - inicial);
    }

    private sealed record SnapshotMemoria(
        long MemoriaGerenciadaBytes,
        long WorkingSetBytes,
        long MemoriaPrivadaBytes)
    {
        public static SnapshotMemoria Capturar()
        {
            using var processo = Process.GetCurrentProcess();

            return new SnapshotMemoria(
                MemoriaGerenciadaBytes: GC.GetTotalMemory(forceFullCollection: false),
                WorkingSetBytes: processo.WorkingSet64,
                MemoriaPrivadaBytes: processo.PrivateMemorySize64);
        }
    }

    private sealed class PicosMemoria(SnapshotMemoria inicial)
    {
        private long _picoMemoriaGerenciadaBytes = inicial.MemoriaGerenciadaBytes;
        private long _picoWorkingSetBytes = inicial.WorkingSetBytes;
        private long _picoMemoriaPrivadaBytes = inicial.MemoriaPrivadaBytes;
        private int _quantidadeAmostras = 1;

        public long PicoMemoriaGerenciadaBytes => Volatile.Read(
            ref _picoMemoriaGerenciadaBytes);

        public long PicoWorkingSetBytes => Volatile.Read(ref _picoWorkingSetBytes);

        public long PicoMemoriaPrivadaBytes => Volatile.Read(ref _picoMemoriaPrivadaBytes);

        public int QuantidadeAmostras => Volatile.Read(ref _quantidadeAmostras);

        public void Registrar(SnapshotMemoria snapshot)
        {
            AtualizarMaximo(
                ref _picoMemoriaGerenciadaBytes,
                snapshot.MemoriaGerenciadaBytes);

            AtualizarMaximo(ref _picoWorkingSetBytes, snapshot.WorkingSetBytes);
            AtualizarMaximo(ref _picoMemoriaPrivadaBytes, snapshot.MemoriaPrivadaBytes);
            Interlocked.Increment(ref _quantidadeAmostras);
        }

        private static void AtualizarMaximo(ref long destino, long candidato)
        {
            var atual = Volatile.Read(ref destino);

            while (candidato > atual)
            {
                var anterior = Interlocked.CompareExchange(ref destino, candidato, atual);

                if (anterior == atual)
                {
                    return;
                }

                atual = anterior;
            }
        }
    }

    private sealed class ArtefatoMedicao : IDisposable
    {
        private object? _referenciaRetida;
        private IDisposable? _recursoDescartavel;
        private string? _caminhoTemporario;

        private ArtefatoMedicao(
            long tamanhoBytes,
            string destino,
            object? referenciaRetida = null,
            IDisposable? recursoDescartavel = null,
            string? caminhoTemporario = null)
        {
            TamanhoBytes = tamanhoBytes;
            Destino = destino;
            _referenciaRetida = referenciaRetida;
            _recursoDescartavel = recursoDescartavel;
            _caminhoTemporario = caminhoTemporario;
        }

        public long TamanhoBytes { get; }

        public string Destino { get; }

        public static ArtefatoMedicao ParaArray(byte[] conteudo, string destino)
        {
            return new ArtefatoMedicao(
                conteudo.LongLength,
                destino,
                referenciaRetida: conteudo);
        }

        public static ArtefatoMedicao ParaStream(Stream stream, string destino)
        {
            return new ArtefatoMedicao(
                stream.Length,
                destino,
                recursoDescartavel: stream);
        }

        public static ArtefatoMedicao ParaArquivoTemporario(string caminho)
        {
            return new ArtefatoMedicao(
                new FileInfo(caminho).Length,
                "arquivo temporário em disco",
                caminhoTemporario: caminho);
        }

        public void Dispose()
        {
            _recursoDescartavel?.Dispose();

            if (_caminhoTemporario is not null)
            {
                File.Delete(_caminhoTemporario);
            }

            _referenciaRetida = null;
            _recursoDescartavel = null;
            _caminhoTemporario = null;
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
