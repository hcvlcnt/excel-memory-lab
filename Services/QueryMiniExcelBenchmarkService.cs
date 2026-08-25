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
    private const int SamplingIntervalMs = 10;
    private const int WarmUpQuantity = 100;
    private readonly SemaphoreSlim _measurementLock = new(1, 1);

    private static readonly QueryMiniExcelScenario[] Scenarios =
    [
        new(
            QueryMiniExcelScenarios.BufferedClient,
            "EF Core executa a parte traduzível no SQLite e carrega tudo com ToList().",
            "A descrição do enum é calculada em C# depois da consulta.",
            "Lista completa na memória antes de o MiniExcel começar.",
            "Reproduzir o gargalo causado pela materialização antecipada."),
        new(
            QueryMiniExcelScenarios.ClientStreaming,
            "EF Core mantém filtros, ordenação e projeção simples no SQLite.",
            "AsEnumerable cria a fronteira; a descrição do enum é calculada em C# por linha.",
            "IEnumerable adiado é entregue diretamente ao MiniExcel, sem ToList().",
            "Resolver método não traduzível sem perder o streaming."),
        new(
            QueryMiniExcelScenarios.StreamingSqlCase,
            "EF Core projeta a descrição do status com CASE diretamente no SQLite.",
            "Descrição e valor do estoque são calculados por expressões traduzíveis no SQL.",
            "IEnumerable adiado é entregue diretamente ao MiniExcel.",
            "Comparar conversão no banco com conversão cliente em streaming."),
        new(
            QueryMiniExcelScenarios.DirectDbReader,
            "DbDataReader forward-only executa SQL manual com CASE e multiplicação.",
            "Descrição e valor do estoque chegam prontos do SQLite.",
            "IDataReader é entregue diretamente ao MiniExcel, sem DTO ou coleção.",
            "Medir o menor pipeline possível entre banco e gerador de Excel."),
        new(
            QueryMiniExcelScenarios.ProcessedDbReader,
            "DbDataReader forward-only entrega apenas os valores brutos necessários.",
            "Um iterador traduz o enum e calcula o valor do estoque linha a linha em C#.",
            "yield return entrega uma única linha de cada vez ao MiniExcel.",
            "Medir processamento cliente flexível com memória auxiliar constante.")
    ];

    public IReadOnlyCollection<QueryMiniExcelScenario> GetScenarios() => Scenarios;

    public async Task<QueryTranslationDiagnostic> DiagnoseTranslationAsync(
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseAsync(1, cancellationToken);
        await using var context = CreateContext();

        const string expression =
            "context.InventoryItems.Where(item => TranslateStatus(item.Status).Contains(\"Disponível\"))";

        try
        {
            var sql = context.InventoryItems
                .Where(item => TranslateStatus(item.Status).Contains("Disponível"))
                .ToQueryString();

            return new QueryTranslationDiagnostic(
                true,
                expression,
                sql,
                "Nenhuma correção foi necessária.");
        }
        catch (InvalidOperationException exception)
        {
            return new QueryTranslationDiagnostic(
                false,
                expression,
                exception.Message,
                "Mantenha Where/OrderBy/GroupBy traduzíveis antes de AsEnumerable(). " +
                "Converta o enum depois dessa fronteira ou use uma expressão condicional " +
                "traduzível para SQL CASE quando a descrição precisar participar da consulta.");
        }
    }

    public async Task<QueryMiniExcelBenchmarkSummary> BenchmarkAsync(
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
            await EnsureDatabaseAsync(quantity, cancellationToken);

            if (discardWarmUpRun)
            {
                await MeasureCoreAsync(
                    scenario,
                    quantity,
                    prepareDatabase: false,
                    warmUp: false,
                    forceGc,
                    cancellationToken);
            }

            var runs = new List<QueryMiniExcelBenchmarkResult>(repetitions);

            for (var repetition = 0; repetition < repetitions; repetition++)
            {
                runs.Add(await MeasureCoreAsync(
                    scenario,
                    quantity,
                    prepareDatabase: false,
                    warmUp: false,
                    forceGc,
                    cancellationToken));
            }

            return QueryMiniExcelBenchmarkSummary.From(runs, discardWarmUpRun);
        }
        finally
        {
            _measurementLock.Release();
        }
    }

    public async Task<QueryMiniExcelBenchmarkResult> MeasureAsync(
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
                prepareDatabase: true,
                warmUp,
                forceGc,
                cancellationToken);
        }
        finally
        {
            _measurementLock.Release();
        }
    }

    private async Task<QueryMiniExcelBenchmarkResult> MeasureCoreAsync(
        string scenario,
        int quantity,
        bool prepareDatabase,
        bool warmUp,
        bool forceGc,
        CancellationToken cancellationToken)
    {
        if (!QueryMiniExcelScenarios.All.Contains(scenario))
        {
            throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Cenário desconhecido.");
        }

        QueryArtifact? artifact = null;

        try
        {
            if (prepareDatabase)
            {
                await EnsureDatabaseAsync(quantity, cancellationToken);
            }

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
            var initialGeneration0Count = GC.CollectionCount(0);
            var initialGeneration1Count = GC.CollectionCount(1);
            var initialGeneration2Count = GC.CollectionCount(2);

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
                    // Encerramento normal da amostragem.
                }
            }

            var description = Scenarios.Single(item =>
                string.Equals(item.Name, scenario, StringComparison.OrdinalIgnoreCase));

            return new QueryMiniExcelBenchmarkResult(
                Scenario: scenario,
                Quantity: quantity,
                QueryStrategy: description.Query,
                BuffersResults: scenario.Equals(
                    QueryMiniExcelScenarios.BufferedClient,
                    StringComparison.OrdinalIgnoreCase),
                ClientSideEnumConversion: scenario.Equals(
                    QueryMiniExcelScenarios.BufferedClient,
                    StringComparison.OrdinalIgnoreCase) ||
                    scenario.Equals(
                        QueryMiniExcelScenarios.ClientStreaming,
                        StringComparison.OrdinalIgnoreCase) ||
                    scenario.Equals(
                        QueryMiniExcelScenarios.ProcessedDbReader,
                        StringComparison.OrdinalIgnoreCase),
                GeneratedSql: artifact.Sql,
                TemporaryFile: artifact.FileName,
                SamplingIntervalMs: SamplingIntervalMs,
                SampleCount: peaks.SampleCount,
                DurationMs: Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                FileSizeBytes: artifact.SizeBytes,
                PeakManagedMemoryDeltaBytes: CalculateDelta(
                    peaks.PeakManagedMemoryBytes,
                    initialSnapshot.ManagedMemoryBytes),
                PeakWorkingSetDeltaBytes: CalculateDelta(
                    peaks.PeakWorkingSetBytes,
                    initialSnapshot.WorkingSetBytes),
                PeakPrivateMemoryDeltaBytes: CalculateDelta(
                    peaks.PeakPrivateMemoryBytes,
                    initialSnapshot.PrivateMemoryBytes),
                BytesAllocatedDuringMeasurement: Math.Max(
                    0,
                    GC.GetTotalAllocatedBytes(precise: true) - initialAllocatedBytes),
                Generation0Collections: GC.CollectionCount(0) - initialGeneration0Count,
                Generation1Collections: GC.CollectionCount(1) - initialGeneration1Count,
                Generation2Collections: GC.CollectionCount(2) - initialGeneration2Count);
        }
        finally
        {
            artifact?.Dispose();
        }
    }

    private QueryArtifact ExecuteScenario(
        string scenario,
        int quantity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return scenario.ToLowerInvariant() switch
        {
            QueryMiniExcelScenarios.BufferedClient => ExecuteBuffered(quantity),
            QueryMiniExcelScenarios.ClientStreaming => ExecuteClientStreaming(quantity),
            QueryMiniExcelScenarios.StreamingSqlCase => ExecuteSqlCaseStreaming(quantity),
            QueryMiniExcelScenarios.DirectDbReader => ExecuteDirectDbReader(
                quantity,
                cancellationToken),
            QueryMiniExcelScenarios.ProcessedDbReader => ExecuteProcessedDbReader(
                quantity,
                cancellationToken),
            _ => throw new InvalidOperationException("Cenário não suportado.")
        };
    }

    private QueryArtifact ExecuteBuffered(int quantity)
    {
        using var context = CreateContext();
        var query = CreateRawQuery(context, quantity);
        var sql = query.ToQueryString();
        var items = query.ToList();
        var rows = items.Select(MapOnClient);

        return SaveExcel(rows, sql);
    }

    private QueryArtifact ExecuteClientStreaming(int quantity)
    {
        using var context = CreateContext();
        var query = CreateRawQuery(context, quantity);
        var sql = query.ToQueryString();
        var rows = MapAsStream(query.AsEnumerable());

        return SaveExcel(rows, sql);
    }

    private QueryArtifact ExecuteSqlCaseStreaming(int quantity)
    {
        using var context = CreateContext();
        var query = context.InventoryItems
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Take(quantity)
            .Select(item => new InventoryMiniExcelRow
            {
                Id = item.Id,
                Code = item.Code,
                Description = item.Description,
                Status = item.Status == InventoryStatus.Available
                    ? "Disponível"
                    : item.Status == InventoryStatus.Reserved
                        ? "Reservado"
                        : item.Status == InventoryStatus.Blocked
                            ? "Bloqueado"
                            : "Sem saldo",
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                InventoryValue = item.Quantity * item.UnitCost,
                LastMovement = item.LastMovement
            });

        var sql = query.ToQueryString();
        return SaveExcel(query.AsEnumerable(), sql);
    }

    private QueryArtifact ExecuteDirectDbReader(
        int quantity,
        CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = CreateDbReaderCommand(
            connection,
            quantity,
            processOnClient: false);
        using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);
        cancellationToken.ThrowIfCancellationRequested();

        return SaveExcel(reader, FormatDbReaderSql(command));
    }

    private QueryArtifact ExecuteProcessedDbReader(
        int quantity,
        CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = CreateDbReaderCommand(
            connection,
            quantity,
            processOnClient: true);
        using var reader = command.ExecuteReader(CommandBehavior.SequentialAccess);
        var rows = MapDbReaderAsStream(reader, cancellationToken);

        return SaveExcel(rows, FormatDbReaderSql(command));
    }

    private static SqliteCommand CreateDbReaderCommand(
        SqliteConnection connection,
        int quantity,
        bool processOnClient)
    {
        var command = connection.CreateCommand();
        command.CommandText = processOnClient
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

        command.Parameters.AddWithValue("$quantidade", quantity);
        return command;
    }

    private static string FormatDbReaderSql(SqliteCommand command)
    {
        var quantity = command.Parameters["$quantidade"].Value;
        return $"-- $quantidade = {quantity}{Environment.NewLine}{command.CommandText}";
    }

    private static IQueryable<RawInventoryQuery> CreateRawQuery(
        QueryBenchmarkDbContext context,
        int quantity)
    {
        return context.InventoryItems
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Take(quantity)
            .Select(item => new RawInventoryQuery
            {
                Id = item.Id,
                Code = item.Code,
                Description = item.Description,
                Status = item.Status,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                LastMovement = item.LastMovement
            });
    }

    private static IEnumerable<InventoryMiniExcelRow> MapAsStream(
        IEnumerable<RawInventoryQuery> items)
    {
        foreach (var item in items)
        {
            yield return MapOnClient(item);
        }
    }

    private static IEnumerable<InventoryMiniExcelRow> MapDbReaderAsStream(
        SqliteDataReader reader,
        CancellationToken cancellationToken)
    {
        var idOrdinal = reader.GetOrdinal("Id");
        var codeOrdinal = reader.GetOrdinal("Codigo");
        var descriptionOrdinal = reader.GetOrdinal("Descricao");
        var statusOrdinal = reader.GetOrdinal("Status");
        var quantityOrdinal = reader.GetOrdinal("Quantidade");
        var costOrdinal = reader.GetOrdinal("CustoUnitario");
        var lastMovementOrdinal = reader.GetOrdinal("UltimaMovimentacao");

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quantity = reader.GetInt32(quantityOrdinal);
            var cost = Convert.ToDecimal(
                reader.GetValue(costOrdinal),
                CultureInfo.InvariantCulture);

            yield return new InventoryMiniExcelRow
            {
                Id = reader.GetInt64(idOrdinal),
                Code = reader.GetString(codeOrdinal),
                Description = reader.GetString(descriptionOrdinal),
                Status = TranslateStatus((InventoryStatus)reader.GetInt32(statusOrdinal)),
                Quantity = quantity,
                UnitCost = cost,
                InventoryValue = quantity * cost,
                LastMovement = reader.GetDateTime(lastMovementOrdinal)
            };
        }
    }

    private static InventoryMiniExcelRow MapOnClient(RawInventoryQuery item)
    {
        return new InventoryMiniExcelRow
        {
            Id = item.Id,
            Code = item.Code,
            Description = item.Description,
            Status = TranslateStatus(item.Status),
            Quantity = item.Quantity,
            UnitCost = item.UnitCost,
            InventoryValue = item.Quantity * item.UnitCost,
            LastMovement = item.LastMovement
        };
    }

    private static string TranslateStatus(InventoryStatus status)
    {
        return status switch
        {
            InventoryStatus.Available => "Disponível",
            InventoryStatus.Reserved => "Reservado",
            InventoryStatus.Blocked => "Bloqueado",
            InventoryStatus.OutOfStock => "Sem saldo",
            _ => "Desconhecido"
        };
    }

    private static QueryArtifact SaveExcel(object rows, string sql)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"query-miniexcel-{Guid.NewGuid():N}.xlsx");

        MiniExcel.SaveAs(path, rows, printHeader: true, sheetName: "Estoque");
        return new QueryArtifact(path, sql);
    }

    private QueryBenchmarkDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<QueryBenchmarkDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new QueryBenchmarkDbContext(options);
    }

    private async Task EnsureDatabaseAsync(int quantity, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var context = CreateContext())
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COALESCE(MAX(Id), 0) FROM Estoques";
        var current = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        if (current >= quantity)
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
        var codeParameter = insert.Parameters.Add("$codigo", SqliteType.Text);
        var descriptionParameter = insert.Parameters.Add("$descricao", SqliteType.Text);
        var statusParameter = insert.Parameters.Add("$status", SqliteType.Integer);
        var quantityParameter = insert.Parameters.Add("$quantidade", SqliteType.Integer);
        var costParameter = insert.Parameters.Add("$custo", SqliteType.Real);
        var dateParameter = insert.Parameters.Add("$data", SqliteType.Text);
        insert.Prepare();

        for (var id = current + 1; id <= quantity; id++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            idParameter.Value = id;
            codeParameter.Value = $"SKU-{id:0000000}";
            descriptionParameter.Value = $"Produto de benchmark {id:0000000}";
            statusParameter.Value = ((id - 1) % 4) + 1;
            quantityParameter.Value = id % 500;
            costParameter.Value = Math.Round(1.25 + (id % 10_000) / 13.0, 2);
            dateParameter.Value = new DateTime(2020, 1, 1)
                .AddMinutes(id)
                .ToString("O");

            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SampleAsync(MemoryPeaks peaks, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            peaks.Record(MemorySnapshot.Capture());
            await Task.Delay(SamplingIntervalMs, cancellationToken);
        }
    }

    private static void ForceFullCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
    }

    private static long CalculateDelta(long peak, long initial) => Math.Max(0, peak - initial);

    private sealed record MemorySnapshot(
        long ManagedMemoryBytes,
        long WorkingSetBytes,
        long PrivateMemoryBytes)
    {
        public static MemorySnapshot Capture()
        {
            using var process = Process.GetCurrentProcess();
            return new MemorySnapshot(
                GC.GetTotalMemory(false),
                process.WorkingSet64,
                process.PrivateMemorySize64);
        }
    }

    private sealed class MemoryPeaks(MemorySnapshot initial)
    {
        private long _managedMemory = initial.ManagedMemoryBytes;
        private long _workingSet = initial.WorkingSetBytes;
        private long _privateMemory = initial.PrivateMemoryBytes;
        private int _samples = 1;

        public long PeakManagedMemoryBytes => Volatile.Read(ref _managedMemory);
        public long PeakWorkingSetBytes => Volatile.Read(ref _workingSet);
        public long PeakPrivateMemoryBytes => Volatile.Read(ref _privateMemory);
        public int SampleCount => Volatile.Read(ref _samples);

        public void Record(MemorySnapshot snapshot)
        {
            UpdateMaximum(ref _managedMemory, snapshot.ManagedMemoryBytes);
            UpdateMaximum(ref _workingSet, snapshot.WorkingSetBytes);
            UpdateMaximum(ref _privateMemory, snapshot.PrivateMemoryBytes);
            Interlocked.Increment(ref _samples);
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

    private sealed class QueryArtifact : IDisposable
    {
        private string? _path;

        public QueryArtifact(string path, string sql)
        {
            _path = path;
            Sql = sql;
            SizeBytes = new FileInfo(path).Length;
            FileName = Path.GetFileName(path);
        }

        public string Sql { get; }
        public long SizeBytes { get; }
        public string FileName { get; }

        public void Dispose()
        {
            if (_path is not null && File.Exists(_path))
            {
                File.Delete(_path);
            }

            _path = null;
        }
    }
}
