namespace OutOfMemoryWorkbook.Models;

public sealed record CenarioQueryMiniExcel(
    string Nome,
    string Consulta,
    string ConversaoEnum,
    string Materializacao,
    string Objetivo);
