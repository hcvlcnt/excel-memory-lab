namespace OutOfMemoryWorkbook.Models;

public static class CenariosExportacao
{
    public const string Atual = "atual";
    public const string XssfSemToArray = "xssf-sem-to-array";
    public const string SxssfComLista = "sxssf-com-lista";
    public const string SxssfStreamArquivo = "sxssf-stream-arquivo";
    public const string SxssfStreamResponse = "sxssf-stream-response";

    public static IReadOnlySet<string> Todos { get; } = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        Atual,
        XssfSemToArray,
        SxssfComLista,
        SxssfStreamArquivo,
        SxssfStreamResponse
    };
}
