using System.Text.Json;
using OutOfMemoryWorkbook.Models;

namespace OutOfMemoryWorkbook.Services;

public static class ExportBenchmarkCommand
{
    private const int MaximumRecords = 1_048_575;

    public static bool WasRequested(string[] args)
    {
        return args.Length > 0 &&
               string.Equals(args[0], "benchmark", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> ExecuteAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = ParseOptions(args);
            var dataSource = new InventoryDataSource();
            var exportService = new InventoryExportService(dataSource);
            var measurementService = new ExportMeasurementService(exportService);

            var result = await measurementService.MeasureAsync(
                options.Scenario,
                options.Quantity,
                options.WarmUp,
                options.ForceGc,
                cancellationToken);

            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            };

            await Console.Out.WriteLineAsync(JsonSerializer.Serialize(result, jsonOptions));
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static BenchmarkOptions ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--"))
            {
                throw new ArgumentException(
                    "Uso: benchmark --cenario <nome> --quantidade <n> --aquecer <true|false> --forcar-gc <true|false>.");
            }

            values[args[index][2..]] = args[index + 1];
        }

        var scenario = GetValue(values, "cenario", ExportScenarios.Current);

        if (!ExportScenarios.All.Contains(scenario))
        {
            throw new ArgumentException(
                $"Cenário desconhecido. Valores aceitos: {string.Join(", ", ExportScenarios.All)}.");
        }

        if (!int.TryParse(GetValue(values, "quantidade", "100000"), out var quantity) ||
            quantity is < 1 or > MaximumRecords)
        {
            throw new ArgumentException(
                $"Quantidade deve estar entre 1 e {MaximumRecords}.");
        }

        var warmUp = ParseBoolean(values, "aquecer", defaultValue: true);
        var forceGc = ParseBoolean(values, "forcar-gc", defaultValue: true);

        return new BenchmarkOptions(scenario, quantity, warmUp, forceGc);
    }

    private static string GetValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        string defaultValue)
    {
        return values.TryGetValue(key, out var value) ? value : defaultValue;
    }

    private static bool ParseBoolean(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool defaultValue)
    {
        if (!values.TryGetValue(key, out var text))
        {
            return defaultValue;
        }

        if (!bool.TryParse(text, out var value))
        {
            throw new ArgumentException($"O argumento --{key} deve ser true ou false.");
        }

        return value;
    }

    private sealed record BenchmarkOptions(
        string Scenario,
        int Quantity,
        bool WarmUp,
        bool ForceGc);
}
