namespace OutOfMemoryWorkbook.Models;

public sealed record DiagnosticoTraducaoQuery(
    bool Traduzivel,
    string Expressao,
    string Mensagem,
    string SolucaoRecomendada);
