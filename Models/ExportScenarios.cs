namespace OutOfMemoryWorkbook.Models;

public static class ExportScenarios
{
    public const string Current = "atual";
    public const string XssfWithoutToArray = "xssf-sem-to-array";
    public const string SxssfWithList = "sxssf-com-lista";
    public const string SxssfFileStream = "sxssf-stream-arquivo";
    public const string SxssfResponseStream = "sxssf-stream-response";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        Current,
        XssfWithoutToArray,
        SxssfWithList,
        SxssfFileStream,
        SxssfResponseStream
    };
}
