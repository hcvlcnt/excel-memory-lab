using Microsoft.AspNetCore.Mvc;
using OutOfMemoryWorkbook.Models;
using OutOfMemoryWorkbook.Services;

namespace OutOfMemoryWorkbook.Controllers;

[ApiController]
[Route("api/benchmarks/query-miniexcel")]
public sealed class QueryMiniExcelBenchmarksController(
    IQueryMiniExcelBenchmarkService benchmarkService) : ControllerBase
{
    private const int MaximumRecords = 1_048_575;

    [HttpGet("cenarios")]
    public ActionResult<IReadOnlyCollection<QueryMiniExcelScenario>> GetScenarios()
    {
        return Ok(benchmarkService.GetScenarios());
    }

    [HttpGet("diagnostico")]
    public async Task<ActionResult<QueryTranslationDiagnostic>> DiagnoseAsync(
        CancellationToken cancellationToken)
    {
        return Ok(await benchmarkService.DiagnoseTranslationAsync(cancellationToken));
    }

    [HttpPost("{cenario}")]
    public async Task<ActionResult<QueryMiniExcelBenchmarkSummary>> MeasureAsync(
        [FromRoute(Name = "cenario")] string scenario,
        [FromQuery(Name = "quantidade")] int quantity = 100_000,
        [FromQuery(Name = "repeticoes")] int repetitions = 5,
        [FromQuery(Name = "aquecer")] bool warmUp = true,
        [FromQuery(Name = "forcarGc")] bool forceGc = true,
        CancellationToken cancellationToken = default)
    {
        if (!QueryMiniExcelScenarios.All.Contains(scenario))
        {
            return BadRequest(new
            {
                message = "Cenário desconhecido.",
                scenarios = QueryMiniExcelScenarios.All
            });
        }

        if (quantity is < 1 or > MaximumRecords)
        {
            return BadRequest(new
            {
                message = $"Quantidade deve estar entre 1 e {MaximumRecords}."
            });
        }

        if (repetitions is < 1 or > 10)
        {
            return BadRequest(new
            {
                message = "A quantidade de repetições deve estar entre 1 e 10."
            });
        }

        return Ok(await benchmarkService.BenchmarkAsync(
            scenario,
            quantity,
            repetitions,
            warmUp,
            forceGc,
            cancellationToken));
    }
}
