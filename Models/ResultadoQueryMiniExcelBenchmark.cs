namespace OutOfMemoryWorkbook.Models;

public sealed record ResultadoQueryMiniExcelBenchmark(
    string Cenario,
    int Quantidade,
    string EstrategiaConsulta,
    bool BufferizaResultados,
    bool ConversaoEnumNoCliente,
    string SqlGerado,
    string ArquivoTemporario,
    int IntervaloAmostragemMs,
    int QuantidadeAmostras,
    double DuracaoMs,
    long TamanhoArquivoBytes,
    long DeltaPicoMemoriaGerenciadaBytes,
    long DeltaPicoWorkingSetBytes,
    long DeltaPicoMemoriaPrivadaBytes,
    long BytesAlocadosDuranteMedicao,
    int ColetasGeracao0,
    int ColetasGeracao1,
    int ColetasGeracao2)
{
    public double TamanhoArquivoMiB => ConverterParaMiB(TamanhoArquivoBytes);

    public double DeltaPicoMemoriaGerenciadaMiB => ConverterParaMiB(
        DeltaPicoMemoriaGerenciadaBytes);

    public double DeltaPicoWorkingSetMiB => ConverterParaMiB(DeltaPicoWorkingSetBytes);

    public double DeltaPicoMemoriaPrivadaMiB => ConverterParaMiB(DeltaPicoMemoriaPrivadaBytes);

    public double AlocadoDuranteMedicaoMiB => ConverterParaMiB(BytesAlocadosDuranteMedicao);

    private static double ConverterParaMiB(long bytes)
    {
        return Math.Round(bytes / 1024d / 1024d, 2);
    }
}
