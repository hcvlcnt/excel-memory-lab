# Benchmark EF Core + MiniExcel

Este eixo do laboratório separa dois problemas que normalmente aparecem juntos:

1. tradução da consulta LINQ pelo EF Core;
2. materialização dos dados antes de gerar o Excel.

O MiniExcel reduz o custo da criação do `.xlsx`, mas não torna métodos C# arbitrários
traduzíveis para SQL. O desenho recomendado é manter filtros, junções, ordenação e
agrupamentos no banco e mover somente a formatação de saída para depois da fronteira
`AsEnumerable()`.

## Cenários

| Cenário | Consulta | Conversão do enum | Materialização |
|---|---|---|---|
| `bufferizado-cliente` | Projeção simples no SQLite | Método C# | `ToList()` antes do MiniExcel |
| `streaming-cliente` | Projeção simples no SQLite | Método C# depois de `AsEnumerable()` | `IEnumerable` adiado direto no MiniExcel |
| `streaming-sql-case` | Projeção com `CASE` no SQLite | No SQL | `IEnumerable` adiado direto no MiniExcel |
| `dbreader-direto` | SQL manual com `CASE` e cálculo | No SQL | `IDataReader` direto no MiniExcel |
| `dbreader-processado` | SQL manual com valores brutos | Método C# por linha | `yield return` direto no MiniExcel |

Os cinco cenários consultam os mesmos registros e produzem as mesmas oito colunas,
incluindo `ValorEmEstoque`. O cálculo é feito no SQL nos cenários `streaming-sql-case`
e `dbreader-direto`, e no cliente nos demais cenários.
O banco SQLite é preenchido deterministicamente fora do trecho medido.

## Executar uma medição isolada

```powershell
dotnet build -c Release
dotnet .\bin\Release\net10.0\OutOfMemoryWorkbook.dll query-benchmark `
  --cenario streaming-cliente `
  --quantidade 100000 `
  --aquecer true `
  --forcar-gc true
```

Cada execução ocorre em um processo novo quando chamada pelo script, evitando que um
cenário herde heap, caches e fragmentação do cenário anterior.

## Executar a bateria

```powershell
.\Scripts\medir-query-miniexcel.ps1 -Repeticoes 5
```

Para um ensaio rápido:

```powershell
.\Scripts\medir-query-miniexcel.ps1 -Quantidades 50000 -Repeticoes 1
```

O CSV é gravado em `Resultados`.

## Endpoints

- `GET /api/benchmarks/query-miniexcel/cenarios`
- `GET /api/benchmarks/query-miniexcel/diagnostico`
- `POST /api/benchmarks/query-miniexcel/{cenario}?quantidade=100000&aquecer=true&forcarGc=true`

O diagnóstico executa propositalmente um método `TraduzirStatus` dentro de `Where` e
captura a falha de tradução. O cenário `streaming-sql-case` também devolve o SQL gerado,
permitindo confirmar a presença do `CASE`.

No cenário `dbreader-processado`, conexão, comando e reader permanecem abertos enquanto
o MiniExcel enumera o iterador. Cada chamada a `Read()` cria apenas a linha atual; não
há `List`, `DataTable` ou buffer contendo o resultado inteiro. No `dbreader-direto`, o
próprio `IDataReader` é entregue ao MiniExcel.

## Resultado preliminar

Uma execução de validação com 50 mil linhas e as oito colunas produziu:

| Cenário | Pico gerenciado | Working set | Tempo | Arquivo |
|---|---:|---:|---:|---:|
| Bufferizado no cliente | 53,58 MiB | 77,12 MiB | 826,20 ms | 3,98 MiB |
| Streaming no cliente | 27,04 MiB | 33,56 MiB | 812,90 ms | 3,98 MiB |
| Streaming com SQL CASE | 22,79 MiB | 26,21 MiB | 927,40 ms | 3,99 MiB |
| DbDataReader direto | 16,77 MiB | 20,89 MiB | 615,05 ms | 4,04 MiB |
| DbDataReader processado | 17,24 MiB | 22,31 MiB | 628,01 ms | 3,98 MiB |

O reader processado também foi medido com volumes diferentes:

| Linhas | Pico gerenciado | Total alocado | Tempo |
|---:|---:|---:|---:|
| 10 mil | 14,17 MiB | 48,00 MiB | 137,20 ms |
| 50 mil | 17,24 MiB | 179,40 MiB | 628,01 ms |
| 100 mil | 13,91 MiB | 333,85 MiB | 891,91 ms |

O total alocado cresce com a quantidade porque cada linha ainda cria objetos. Já o pico
permaneceu na mesma faixa porque esses objetos deixam de estar vivos depois de escritos.
Isso é compatível com memória auxiliar aproximadamente `O(1)` em relação à quantidade,
mas não prova uma garantia formal de memória constante de todo o pipeline.

Esses resultados têm uma única repetição e servem como validação funcional. Para números
publicáveis, use pelo menos cinco repetições por cenário, descarte ou identifique outliers
e apresente mediana, percentis e ambiente de execução.

## Decisão prática

- Se a descrição do enum só aparece no Excel, converta depois de `AsEnumerable()`.
- Para o menor pipeline possível, use `IDataReader` direto; para regras de apresentação
  mais flexíveis, envolva o reader em um iterador com `yield return`.
- Se ela participa de `Where`, `OrderBy` ou `GroupBy`, use uma expressão traduzível
  para `CASE` ou normalize os estados em uma tabela e faça `JOIN`.
- `HasConversion<string>()` é uma decisão de persistência; ela não ensina o EF Core a
  traduzir um método como `Enum.GetDescription()`.
- Não use `ToList()` apenas para contornar o erro de tradução em exportações grandes.

Referências: [avaliação cliente e servidor no EF Core](https://learn.microsoft.com/ef/core/querying/client-eval),
[conversões de valores no EF Core](https://learn.microsoft.com/ef/core/modeling/value-conversions),
[consultas eficientes e streaming](https://learn.microsoft.com/ef/core/performance/efficient-querying) e
[documentação oficial do MiniExcel](https://github.com/mini-software/MiniExcel/blob/master/README.md).
