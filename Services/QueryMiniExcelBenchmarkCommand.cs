using System.Text.Json;

namespace OutOfMemoryWorkbook.Services;

public static class QueryMiniExcelBenchmarkCommand
{
    private const int MaximoDeRegistros = 1_048_575;

    public static bool FoiSolicitado(string[] args)
    {
        return args.Length > 0 &&
               string.Equals(args[0], "query-benchmark", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> ExecutarAsync(
        string[] args,
        string contentRoot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var opcoes = InterpretarOpcoes(args);
            var databasePath = Path.Combine(contentRoot, "work", "query-benchmark.db");
            var service = new QueryMiniExcelBenchmarkService(databasePath);
            var resultado = await service.MedirAsync(
                opcoes.Cenario,
                opcoes.Quantidade,
                opcoes.Aquecer,
                opcoes.ForcarGc,
                cancellationToken);

            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            };

            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(resultado, jsonOptions));
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.ToString());
            return 1;
        }
    }

    private static Opcoes InterpretarOpcoes(string[] args)
    {
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--"))
            {
                throw new ArgumentException(
                    "Uso: query-benchmark --cenario <nome> --quantidade <n> " +
                    "--aquecer <true|false> --forcar-gc <true|false>.");
            }

            valores[args[index][2..]] = args[index + 1];
        }

        var cenario = ObterValor(
            valores,
            "cenario",
            Models.CenariosQueryMiniExcel.StreamingCliente);

        if (!Models.CenariosQueryMiniExcel.Todos.Contains(cenario))
        {
            throw new ArgumentException(
                $"Cenário desconhecido. Valores aceitos: " +
                $"{string.Join(", ", Models.CenariosQueryMiniExcel.Todos)}.");
        }

        if (!int.TryParse(ObterValor(valores, "quantidade", "100000"), out var quantidade) ||
            quantidade is < 1 or > MaximoDeRegistros)
        {
            throw new ArgumentException($"Quantidade deve estar entre 1 e {MaximoDeRegistros}.");
        }

        return new Opcoes(
            cenario,
            quantidade,
            InterpretarBooleano(valores, "aquecer", true),
            InterpretarBooleano(valores, "forcar-gc", true));
    }

    private static string ObterValor(
        IReadOnlyDictionary<string, string> valores,
        string chave,
        string padrao) => valores.TryGetValue(chave, out var valor) ? valor : padrao;

    private static bool InterpretarBooleano(
        IReadOnlyDictionary<string, string> valores,
        string chave,
        bool padrao)
    {
        if (!valores.TryGetValue(chave, out var texto))
        {
            return padrao;
        }

        return bool.TryParse(texto, out var valor)
            ? valor
            : throw new ArgumentException($"O argumento --{chave} deve ser true ou false.");
    }

    private sealed record Opcoes(string Cenario, int Quantidade, bool Aquecer, bool ForcarGc);
}
