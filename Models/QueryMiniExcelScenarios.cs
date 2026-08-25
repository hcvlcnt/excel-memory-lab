namespace OutOfMemoryWorkbook.Models;

public static class QueryMiniExcelScenarios
{
    public const string BufferedClient = "bufferizado-cliente";
    public const string ClientStreaming = "streaming-cliente";
    public const string StreamingSqlCase = "streaming-sql-case";
    public const string DirectDbReader = "dbreader-direto";
    public const string ProcessedDbReader = "dbreader-processado";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        BufferedClient,
        ClientStreaming,
        StreamingSqlCase,
        DirectDbReader,
        ProcessedDbReader
    };
}
