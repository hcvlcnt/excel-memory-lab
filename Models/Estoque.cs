namespace OutOfMemoryWorkbook.Models;

public sealed record Estoque(
    long Id,
    string Codigo,
    string Descricao,
    string Categoria,
    string Deposito,
    int Quantidade,
    decimal CustoUnitario,
    DateTime UltimaMovimentacao);
