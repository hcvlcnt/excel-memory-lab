using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using OutOfMemoryWorkbook.Models;
using OutOfMemoryWorkbook.Services;

namespace OutOfMemoryWorkbook.Controllers;

[ApiController]
[Route("api/medicoes/estoque")]
public sealed class MedicoesEstoqueController(
    IMedicaoExportacaoService medicaoService) : ControllerBase
{
    private const int MaximoDeRegistros = 1_048_575;

    [HttpPost("{cenario}")]
    public async Task<ActionResult<ResultadoMedicaoExportacao>> Medir(
        [FromRoute] string cenario,
        [FromQuery, Range(1, MaximoDeRegistros)] int quantidade = 100_000,
        [FromQuery] bool aquecer = true,
        [FromQuery] bool forcarGc = true,
        CancellationToken cancellationToken = default)
    {
        if (!CenariosExportacao.Todos.Contains(cenario))
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [nameof(cenario)] =
                    [
                        $"Cenário desconhecido. Valores aceitos: {string.Join(", ", CenariosExportacao.Todos)}."
                    ]
                }));
        }

        var resultado = await medicaoService.MedirAsync(
            cenario,
            quantidade,
            aquecer,
            forcarGc,
            cancellationToken);

        return Ok(resultado);
    }
}
