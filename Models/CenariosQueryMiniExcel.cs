namespace OutOfMemoryWorkbook.Models;

public static class CenariosQueryMiniExcel
{
    public const string BufferizadoCliente = "bufferizado-cliente";
    public const string StreamingCliente = "streaming-cliente";
    public const string StreamingSqlCase = "streaming-sql-case";
    public const string DbReaderDireto = "dbreader-direto";
    public const string DbReaderProcessado = "dbreader-processado";

    public static IReadOnlySet<string> Todos { get; } = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        BufferizadoCliente,
        StreamingCliente,
        StreamingSqlCase,
        DbReaderDireto,
        DbReaderProcessado
    };
}
