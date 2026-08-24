namespace OutOfMemoryWorkbook.Models;

public sealed record CenarioExportacao(
    string Rota,
    string FonteDeDados,
    string Workbook,
    string Destino,
    string Objetivo);
