using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OutOfMemoryWorkbook.Models;
using OutOfMemoryWorkbook.Services;

namespace OutOfMemoryWorkbook.Controllers;

[ApiController]
[Route("api/medicoes/estoque")]
public sealed class InventoryMeasurementsController(
    IExportMeasurementService measurementService) : ControllerBase
{
    private const int MaximumRecords = 1_048_575;

    [HttpPost("{cenario}")]
    public async Task<ActionResult<ExportBenchmarkSummary>> MeasureAsync(
        [FromRoute(Name = "cenario")] string scenario,
        [FromQuery(Name = "quantidade"), Range(1, MaximumRecords,
            ErrorMessage = "Quantidade deve estar entre 1 e 1048575.")] int quantity = 100_000,
        [FromQuery(Name = "repeticoes"), Range(1, 10,
            ErrorMessage = "Repetições deve estar entre 1 e 10.")] int repetitions = 5,
        [FromQuery(Name = "aquecer")] bool warmUp = true,
        [FromQuery(Name = "forcarGc")] bool forceGc = true,
        CancellationToken cancellationToken = default)
    {
        if (!ExportScenarios.All.Contains(scenario))
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(scenario)] =
                    [
                        $"Cenário desconhecido. Valores aceitos: {string.Join(", ", ExportScenarios.All)}."
                    ]
                }));
        }

        var result = await measurementService.BenchmarkAsync(
            scenario,
            quantity,
            repetitions,
            warmUp,
            forceGc,
            cancellationToken);

        return Ok(result);
    }
}
