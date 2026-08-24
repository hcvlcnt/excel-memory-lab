using System.Data;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public sealed class QueryMiniExcelBenchmarkService(string databasePath)
    : IQueryMiniExcelBenchmarkService
{
    private const int IntervaloAmostragemMs = 10;
    private const int QuantidadeAquecimento = 100;
    private readonly SemaphoreSlim _measurementLock = new(1, 1);

    private static readonly CenarioQueryMiniExcel[] Cenarios =
    [
        new(
            CenariosQueryMiniExcel.BufferizadoCliente,
            "EF Core executa a parte traduzível no SQLite e carrega tudo com ToList().",
            "A descrição do enum é calculada em C# depois da consulta.",
            "Lista completa na memória antes de o MiniExcel começar.",
            "Reproduzir o gargalo causado pela materialização antecipada."),
        new(
            CenariosQueryMiniExcel.StreamingCliente,
            "EF Core mantém filtros, ordenação e projeção simples no SQLite.",
            "AsEnumerable cria a fronteira; a descrição do enum é calculada em C# por linha.",
            "IEnumerable adiado é entregue diretamente ao MiniExcel, sem ToList().",
            "Resolver método não traduzível sem perder o streaming."),
        new(
            CenariosQueryMiniExcel.StreamingSqlCase,
            "EF Core projeta a descrição do status com CASE diretamente no SQLite.",
            "Descrição e valor do estoque são calculados por expressões traduzíveis no SQL.",
            "IEnumerable adiado é entregue diretamente ao MiniExcel.",
            "Comparar conversão no banco com conversão cliente em streaming."),
        new(
            CenariosQueryMiniExcel.DbReaderDireto,
            "DbDataReader forward-only executa SQL manual com CASE e multiplicação.",
            "Descrição e valor do estoque chegam prontos do SQLite.",
            "IDataReader é entregue diretamente ao MiniExcel, sem DTO ou coleção.",
            "Medir o menor pipeline possível entre banco e gerador de Excel."),
        new(
            CenariosQueryMiniExcel.DbReaderProcessado,
            "DbDataReader forward-only entrega apenas os valores brutos necessários.",
            "Um iterador traduz o enum e calcula o valor do estoque linha a linha em C#.",
            "yield return entrega uma única linha de cada vez ao MiniExcel.",
            "Medir processamento cliente flexível com memória auxiliar constante.")
    ];

    public IReadOnlyCollection<CenarioQueryMiniExcel> ObterCenarios() => Cenarios;

    public async Task<DiagnosticoTraducaoQuery> DiagnosticarTraducaoAsync(
        CancellationToken cancellationToken)
    {
        await GarantirBancoAsync(1, cancellationToken);
        await using var context = CriarContexto();

        const string expressao =
            "context.Estoques.Where(e => TraduzirStatus(e.Status).Contains(\"Disponível\"))";

        try
        {
            var sql = context.Estoques
                .Where(item => TraduzirStatus(item.Status).Contains("Disponível"))
                .ToQueryString();

            return new DiagnosticoTraducaoQuery(
                true,
                expressao,
                sql,
                "Nenhuma correção foi necessária.");
        }
        catch (InvalidOperationException exception)
        {
            return new DiagnosticoTraducaoQuery(
                false,
                expressao,
                exception.Message,
                "Mantenha Where/OrderBy/GroupBy traduzíveis antes de AsEnumerable(). " +
                "Converta o enum depois dessa fronteira ou use uma expressão condicional " +
                "traduzível para SQL CASE quando a descrição precisar participar da consulta.");
        }
    }

    public async Task<ResultadoQueryMiniExcelBenchmark> MedirAsync(
        string cenario,
        int quantidade,
        bool aquecer,
        bool forcarGc,
        CancellationToken cancellationToken)
    {
        if (!CenariosQueryMiniExcel.Todos.Contains(cenario))
        {
            throw new ArgumentOutOfRangeException(nameof(cenario), cenario, "Cenário desconhecido.");
        }

        await _measurementLock.WaitAsync(cancellationToken);
        ArtefatoQuery? artefato = null;

        try
        {
            await GarantirBancoAsync(quantidade, cancellationToken);

            if (aquecer)
            {
                using var aquecimento = ExecutarCenario(
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
            var geracao0Inicial = GC.CollectionCount(0);
            var geracao1Inicial = GC.CollectionCount(1);
            var geracao2Inicial = GC.CollectionCount(2);

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
                    // Encerramento normal da amostragem.
                }
            }

            var descricao = Cenarios.Single(item =>
                string.Equals(item.Nome, cenario, StringComparison.OrdinalIgnoreCase));

            return new ResultadoQueryMiniExcelBenchmark(
                Cenario: cenario,
                Quantidade: quantidade,
                EstrategiaConsulta: descricao.Consulta,
                BufferizaResultados: cenario.Equals(
                    CenariosQueryMiniExcel.BufferizadoCliente,
                    StringComparison.OrdinalIgnoreCase),
                ConversaoEnumNoCliente: cenario.Equals(
                    CenariosQueryMiniExcel.BufferizadoCliente,
                    StringComparison.OrdinalIgnoreCase) ||
                    cenario.Equals(
                        CenariosQueryMiniExcel.StreamingCliente,
                        StringComparison.OrdinalIgnoreCase) ||
                    cenario.Equals(
                        CenariosQueryMiniExcel.DbReaderProcessado,
                        StringComparison.OrdinalIgnoreCase),
                SqlGerado: artefato.Sql,
                ArquivoTemporario: artefato.NomeArquivo,
                IntervaloAmostragemMs: IntervaloAmostragemMs,
                QuantidadeAmostras: picos.QuantidadeAmostras,
                DuracaoMs: Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                TamanhoArquivoBytes: artefato.TamanhoBytes,
                DeltaPicoMemoriaGerenciadaBytes: CalcularDelta(
                    picos.PicoMemoriaGerenciadaBytes,
                    snapshotInicial.MemoriaGerenciadaBytes),
                DeltaPicoWorkingSetBytes: CalcularDelta(
                    picos.PicoWorkingSetBytes,
                    snapshotInicial.WorkingSetBytes),
                DeltaPicoMemoriaPrivadaBytes: CalcularDelta(
                    picos.PicoMemoriaPrivadaBytes,
                    snapshotInicial.MemoriaPrivadaBytes),
                BytesAlocadosDuranteMedicao: Math.Max(
                    0,
                    GC.GetTotalAllocatedBytes(precise: true) - bytesAlocadosInicial),
                ColetasGeracao0: GC.CollectionCount(0) - geracao0Inicial,
                ColetasGeracao1: GC.CollectionCount(1) - geracao1Inicial,
                ColetasGeracao2: GC.CollectionCount(2) - geracao2Inicial);
        }
        finally
        {
            artefato?.Dispose();
            _measurementLock.Release();
        }
    }

    private ArtefatoQuery ExecutarCenario(
        string cenario,
        int quantidade,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return cenario.ToLowerInvariant() switch
        {
            CenariosQueryMiniExcel.BufferizadoCliente => ExecutarBufferizado(quantidade),
            CenariosQueryMiniExcel.StreamingCliente => ExecutarStreamingCliente(quantidade),
            CenariosQueryMiniExcel.StreamingSqlCase => ExecutarStreamingSqlCase(quantidade),
            CenariosQueryMiniExcel.DbReaderDireto => ExecutarDbReaderDireto(
                quantidade,
                cancellationToken),
            CenariosQueryMiniExcel.DbReaderProcessado => ExecutarDbReaderProcessado(
                quantidade,
                cancellationToken),
            _ => throw new InvalidOperationException("Cenário não suportado.")
        };
    }

    private ArtefatoQuery ExecutarBufferizado(int quantidade)
    {
        using var context = CriarContexto();
        var query = CriarConsultaBruta(context, quantidade);
        var sql = query.ToQueryString();
        var itens = query.ToList();
        var linhas = itens.Select(MapearNoCliente);

        return SalvarExcel(linhas, sql);
    }

    private ArtefatoQuery ExecutarStreamingCliente(int quantidade)
    {
        using var context = CriarContexto();
        var query = CriarConsultaBruta(context, quantidade);
        var sql = query.ToQueryString();
        var linhas = MapearEmStreaming(query.AsEnumerable());

        return SalvarExcel(linhas, sql);
    }

    private ArtefatoQuery ExecutarStreamingSqlCase(int quantidade)
    {
        using var context = CriarContexto();
        var query = context.Estoques
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Take(quantidade)
            .Select(item => new EstoqueMiniExcelRow
            {
                Id = item.Id,
                Codigo = item.Codigo,
                Descricao = item.Descricao,
                Status = item.Status == StatusEstoque.Disponivel
                    ? "Disponível"
                    : item.Status == StatusEstoque.Reservado
                        ? "Reservado"
                        : item.Status == StatusEstoque.Bloqueado
                            ? "Bloqueado"
                            : "Sem saldo",
                Quantidade = item.Quantidade,
                CustoUnitario = item.CustoUnitario,
                ValorEmEstoque = item.Quantidade * item.CustoUnitario,
                UltimaMovimentacao = item.UltimaMovimentacao
            });

        var sql = query.ToQueryString();
        return SalvarExcel(query.AsEnumerable(), sql);
    }

    private ArtefatoQuery ExecutarDbReaderDireto(
        int quantidade,
        CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = CriarComandoDbReader(
            connection,
            quantidade,
            processarNoCliente: false);
        using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);
        cancellationToken.ThrowIfCancellationRequested();

        return SalvarExcel(reader, FormatarSqlDbReader(command));
    }

    private ArtefatoQuery ExecutarDbReaderProcessado(
        int quantidade,
        CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = CriarComandoDbReader(
            connection,
            quantidade,
            processarNoCliente: true);
        using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);
        var linhas = MapearDbReaderEmStreaming(reader, cancellationToken);

        return SalvarExcel(linhas, FormatarSqlDbReader(command));
    }

    private static SqliteCommand CriarComandoDbReader(
        SqliteConnection connection,
        int quantidade,
        bool processarNoCliente)
    {
        var command = connection.CreateCommand();
        command.CommandText = processarNoCliente
            ?
            """
            SELECT
                Id,
                Codigo,
                Descricao,
                Status,
                Quantidade,
                CustoUnitario,
                UltimaMovimentacao
            FROM Estoques
            ORDER BY Id
            LIMIT $quantidade
            """
            :
            """
            SELECT
                Id,
                Codigo,
                Descricao,
                CASE Status
                    WHEN 1 THEN 'Disponível'
                    WHEN 2 THEN 'Reservado'
                    WHEN 3 THEN 'Bloqueado'
                    ELSE 'Sem saldo'
                END AS Status,
                Quantidade,
                ROUND(CAST(CustoUnitario AS REAL), 2) AS CustoUnitario,
                ROUND(Quantidade * CAST(CustoUnitario AS REAL), 2) AS ValorEmEstoque,
                UltimaMovimentacao
            FROM Estoques
            ORDER BY Id
            LIMIT $quantidade
            """;

        command.Parameters.AddWithValue("$quantidade", quantidade);
        return command;
    }

    private static string FormatarSqlDbReader(SqliteCommand command)
    {
        var quantidade = command.Parameters["$quantidade"].Value;
        return $"-- $quantidade = {quantidade}{Environment.NewLine}{command.CommandText}";
    }

    private static IQueryable<EstoqueQueryBruto> CriarConsultaBruta(
        QueryBenchmarkDbContext context,
        int quantidade)
    {
        return context.Estoques
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Take(quantidade)
            .Select(item => new EstoqueQueryBruto
            {
                Id = item.Id,
                Codigo = item.Codigo,
                Descricao = item.Descricao,
                Status = item.Status,
                Quantidade = item.Quantidade,
                CustoUnitario = item.CustoUnitario,
                UltimaMovimentacao = item.UltimaMovimentacao
            });
    }

    private static IEnumerable<EstoqueMiniExcelRow> MapearEmStreaming(
        IEnumerable<EstoqueQueryBruto> itens)
    {
        foreach (var item in itens)
        {
            yield return MapearNoCliente(item);
        }
    }

    private static IEnumerable<EstoqueMiniExcelRow> MapearDbReaderEmStreaming(
        SqliteDataReader reader,
        CancellationToken cancellationToken)
    {
        var idOrdinal = reader.GetOrdinal("Id");
        var codigoOrdinal = reader.GetOrdinal("Codigo");
        var descricaoOrdinal = reader.GetOrdinal("Descricao");
        var statusOrdinal = reader.GetOrdinal("Status");
        var quantidadeOrdinal = reader.GetOrdinal("Quantidade");
        var custoOrdinal = reader.GetOrdinal("CustoUnitario");
        var movimentacaoOrdinal = reader.GetOrdinal("UltimaMovimentacao");

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quantidade = reader.GetInt32(quantidadeOrdinal);
            var custo = Convert.ToDecimal(
                reader.GetValue(custoOrdinal),
                CultureInfo.InvariantCulture);

            yield return new EstoqueMiniExcelRow
            {
                Id = reader.GetInt64(idOrdinal),
                Codigo = reader.GetString(codigoOrdinal),
                Descricao = reader.GetString(descricaoOrdinal),
                Status = TraduzirStatus((StatusEstoque)reader.GetInt32(statusOrdinal)),
                Quantidade = quantidade,
                CustoUnitario = custo,
                ValorEmEstoque = quantidade * custo,
                UltimaMovimentacao = reader.GetDateTime(movimentacaoOrdinal)
            };
        }
    }

    private static EstoqueMiniExcelRow MapearNoCliente(EstoqueQueryBruto item)
    {
        return new EstoqueMiniExcelRow
        {
            Id = item.Id,
            Codigo = item.Codigo,
            Descricao = item.Descricao,
            Status = TraduzirStatus(item.Status),
            Quantidade = item.Quantidade,
            CustoUnitario = item.CustoUnitario,
            ValorEmEstoque = item.Quantidade * item.CustoUnitario,
            UltimaMovimentacao = item.UltimaMovimentacao
        };
    }

    private static string TraduzirStatus(StatusEstoque status)
    {
        return status switch
        {
            StatusEstoque.Disponivel => "Disponível",
            StatusEstoque.Reservado => "Reservado",
            StatusEstoque.Bloqueado => "Bloqueado",
            StatusEstoque.SemSaldo => "Sem saldo",
            _ => "Desconhecido"
        };
    }

    private static ArtefatoQuery SalvarExcel(object linhas, string sql)
    {
        var caminho = Path.Combine(
            Path.GetTempPath(),
            $"query-miniexcel-{Guid.NewGuid():N}.xlsx");

        MiniExcel.SaveAs(caminho, linhas, printHeader: true, sheetName: "Estoque");
        return new ArtefatoQuery(caminho, sql);
    }

    private QueryBenchmarkDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<QueryBenchmarkDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new QueryBenchmarkDbContext(options);
    }

    private async Task GarantirBancoAsync(int quantidade, CancellationToken cancellationToken)
    {
        var diretorio = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(diretorio))
        {
            Directory.CreateDirectory(diretorio);
        }

        await using (var context = CriarContexto())
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COALESCE(MAX(Id), 0) FROM Estoques";
        var atual = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        if (atual >= quantidade)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText =
            """
            INSERT INTO Estoques
                (Id, Codigo, Descricao, Status, Quantidade, CustoUnitario, UltimaMovimentacao)
            VALUES
                ($id, $codigo, $descricao, $status, $quantidade, $custo, $data)
            """;

        var idParameter = insert.Parameters.Add("$id", SqliteType.Integer);
        var codigoParameter = insert.Parameters.Add("$codigo", SqliteType.Text);
        var descricaoParameter = insert.Parameters.Add("$descricao", SqliteType.Text);
        var statusParameter = insert.Parameters.Add("$status", SqliteType.Integer);
        var quantidadeParameter = insert.Parameters.Add("$quantidade", SqliteType.Integer);
        var custoParameter = insert.Parameters.Add("$custo", SqliteType.Real);
        var dataParameter = insert.Parameters.Add("$data", SqliteType.Text);
        insert.Prepare();

        for (var id = atual + 1; id <= quantidade; id++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            idParameter.Value = id;
            codigoParameter.Value = $"SKU-{id:0000000}";
            descricaoParameter.Value = $"Produto de benchmark {id:0000000}";
            statusParameter.Value = ((id - 1) % 4) + 1;
            quantidadeParameter.Value = id % 500;
            custoParameter.Value = Math.Round(1.25 + (id % 10_000) / 13.0, 2);
            dataParameter.Value = new DateTime(2020, 1, 1)
                .AddMinutes(id)
                .ToString("O");

            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task AmostrarAsync(PicosMemoria picos, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            picos.Registrar(SnapshotMemoria.Capturar());
            await Task.Delay(IntervaloAmostragemMs, cancellationToken);
        }
    }

    private static void ForcarColetaCompleta()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }

    private static long CalcularDelta(long pico, long inicial) => Math.Max(0, pico - inicial);

    private sealed record SnapshotMemoria(
        long MemoriaGerenciadaBytes,
        long WorkingSetBytes,
        long MemoriaPrivadaBytes)
    {
        public static SnapshotMemoria Capturar()
        {
            using var processo = Process.GetCurrentProcess();
            return new SnapshotMemoria(
                GC.GetTotalMemory(false),
                processo.WorkingSet64,
                processo.PrivateMemorySize64);
        }
    }

    private sealed class PicosMemoria(SnapshotMemoria inicial)
    {
        private long _gerenciada = inicial.MemoriaGerenciadaBytes;
        private long _workingSet = inicial.WorkingSetBytes;
        private long _privada = inicial.MemoriaPrivadaBytes;
        private int _amostras = 1;

        public long PicoMemoriaGerenciadaBytes => Volatile.Read(ref _gerenciada);
        public long PicoWorkingSetBytes => Volatile.Read(ref _workingSet);
        public long PicoMemoriaPrivadaBytes => Volatile.Read(ref _privada);
        public int QuantidadeAmostras => Volatile.Read(ref _amostras);

        public void Registrar(SnapshotMemoria snapshot)
        {
            AtualizarMaximo(ref _gerenciada, snapshot.MemoriaGerenciadaBytes);
            AtualizarMaximo(ref _workingSet, snapshot.WorkingSetBytes);
            AtualizarMaximo(ref _privada, snapshot.MemoriaPrivadaBytes);
            Interlocked.Increment(ref _amostras);
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

    private sealed class ArtefatoQuery : IDisposable
    {
        private string? _caminho;

        public ArtefatoQuery(string caminho, string sql)
        {
            _caminho = caminho;
            Sql = sql;
            TamanhoBytes = new FileInfo(caminho).Length;
            NomeArquivo = Path.GetFileName(caminho);
        }

        public string Sql { get; }
        public long TamanhoBytes { get; }
        public string NomeArquivo { get; }

        public void Dispose()
        {
            if (_caminho is not null && File.Exists(_caminho))
            {
                File.Delete(_caminho);
            }

            _caminho = null;
        }
    }
}
