using System.Text.Json;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public static class ExportacaoBenchmarkCommand
{
    private const int MaximoDeRegistros = 1_048_575;

    public static bool FoiSolicitado(string[] args)
    {
        return args.Length > 0 &&
               string.Equals(args[0], "benchmark", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> ExecutarAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var opcoes = InterpretarOpcoes(args);
            var dataSource = new EstoqueDataSource();
            var exportService = new EstoqueExportService(dataSource);
            var medicaoService = new MedicaoExportacaoService(exportService);

            var resultado = await medicaoService.MedirAsync(
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
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static OpcoesBenchmark InterpretarOpcoes(string[] args)
    {
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--"))
            {
                throw new ArgumentException(
                    "Uso: benchmark --cenario <nome> --quantidade <n> --aquecer <true|false> --forcar-gc <true|false>.");
            }

            valores[args[index][2..]] = args[index + 1];
        }

        var cenario = ObterValor(valores, "cenario", CenariosExportacao.Atual);

        if (!CenariosExportacao.Todos.Contains(cenario))
        {
            throw new ArgumentException(
                $"Cenário desconhecido. Valores aceitos: {string.Join(", ", CenariosExportacao.Todos)}.");
        }

        if (!int.TryParse(ObterValor(valores, "quantidade", "100000"), out var quantidade) ||
            quantidade is < 1 or > MaximoDeRegistros)
        {
            throw new ArgumentException(
                $"Quantidade deve estar entre 1 e {MaximoDeRegistros}.");
        }

        var aquecer = InterpretarBooleano(valores, "aquecer", valorPadrao: true);
        var forcarGc = InterpretarBooleano(valores, "forcar-gc", valorPadrao: true);

        return new OpcoesBenchmark(cenario, quantidade, aquecer, forcarGc);
    }

    private static string ObterValor(
        IReadOnlyDictionary<string, string> valores,
        string chave,
        string valorPadrao)
    {
        return valores.TryGetValue(chave, out var valor) ? valor : valorPadrao;
    }

    private static bool InterpretarBooleano(
        IReadOnlyDictionary<string, string> valores,
        string chave,
        bool valorPadrao)
    {
        if (!valores.TryGetValue(chave, out var texto))
        {
            return valorPadrao;
        }

        if (!bool.TryParse(texto, out var valor))
        {
            throw new ArgumentException($"O argumento --{chave} deve ser true ou false.");
        }

        return valor;
    }

    private sealed record OpcoesBenchmark(
        string Cenario,
        int Quantidade,
        bool Aquecer,
        bool ForcarGc);
}
