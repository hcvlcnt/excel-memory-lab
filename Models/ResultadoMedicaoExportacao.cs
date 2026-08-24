namespace OutOfMemoryWorkbook.Models;

public sealed record ResultadoMedicaoExportacao(
    string Cenario,
    int Quantidade,
    string DestinoMedicao,
    bool AquecimentoExecutado,
    bool GcForcado,
    int IntervaloAmostragemMs,
    int QuantidadeAmostras,
    double DuracaoMs,
    long TamanhoArquivoBytes,
    long MemoriaGerenciadaInicialBytes,
    long PicoMemoriaGerenciadaBytes,
    long DeltaPicoMemoriaGerenciadaBytes,
    long WorkingSetInicialBytes,
    long PicoWorkingSetBytes,
    long DeltaPicoWorkingSetBytes,
    long MemoriaPrivadaInicialBytes,
    long PicoMemoriaPrivadaBytes,
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
