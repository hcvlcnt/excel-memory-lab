using Microsoft.AspNetCore.Mvc;
using OutOfMemoryWorkbook.Models;
using OutOfMemoryWorkbook.Services;

namespace OutOfMemoryWorkbook.Controllers;

[ApiController]
[Route("api/benchmarks/query-miniexcel")]
public sealed class QueryMiniExcelBenchmarksController(
    IQueryMiniExcelBenchmarkService benchmarkService) : ControllerBase
{
    private const int MaximoDeRegistros = 1_048_575;

    [HttpGet("cenarios")]
    public ActionResult<IReadOnlyCollection<CenarioQueryMiniExcel>> ObterCenarios()
    {
        return Ok(benchmarkService.ObterCenarios());
    }

    [HttpGet("diagnostico")]
    public async Task<ActionResult<DiagnosticoTraducaoQuery>> DiagnosticarAsync(
        CancellationToken cancellationToken)
    {
        return Ok(await benchmarkService.DiagnosticarTraducaoAsync(cancellationToken));
    }

    [HttpPost("{cenario}")]
    public async Task<ActionResult<ResultadoQueryMiniExcelBenchmark>> MedirAsync(
        string cenario,
        [FromQuery] int quantidade = 100_000,
        [FromQuery] bool aquecer = true,
        [FromQuery] bool forcarGc = true,
        CancellationToken cancellationToken = default)
    {
        if (!CenariosQueryMiniExcel.Todos.Contains(cenario))
        {
            return BadRequest(new
            {
                mensagem = "Cenário desconhecido.",
                cenarios = CenariosQueryMiniExcel.Todos
            });
        }

        if (quantidade is < 1 or > MaximoDeRegistros)
        {
            return BadRequest(new
            {
                mensagem = $"Quantidade deve estar entre 1 e {MaximoDeRegistros}."
            });
        }

        return Ok(await benchmarkService.MedirAsync(
            cenario,
            quantidade,
            aquecer,
            forcarGc,
            cancellationToken));
    }
}
