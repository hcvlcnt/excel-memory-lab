using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using OutOfMemoryWorkbook.Models;
using OutOfMemoryWorkbook.Services;

namespace OutOfMemoryWorkbook.Controllers;

[ApiController]
[Route("api/exportacoes/estoque")]
public sealed class ExportacoesEstoqueController(
    IEstoqueExportService exportService) : ControllerBase
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int MaximoDeRegistros = 1_048_575;

    [HttpGet("cenarios")]
    public ActionResult<IReadOnlyCollection<CenarioExportacao>> ObterCenarios()
    {
        CenarioExportacao[] cenarios =
        [
            new(
                Rota: CenariosExportacao.Atual,
                FonteDeDados: "List<Estoque>",
                Workbook: "XSSFWorkbook",
                Destino: "MemoryStream.ToArray()",
                Objetivo: "Reproduzir o maior consumo de memória do fluxo atual."),
            new(
                Rota: CenariosExportacao.XssfSemToArray,
                FonteDeDados: "List<Estoque>",
                Workbook: "XSSFWorkbook",
                Destino: "MemoryStream retornado como Stream",
                Objetivo: "Isolar apenas o custo da cópia criada pelo ToArray()."),
            new(
                Rota: CenariosExportacao.SxssfComLista,
                FonteDeDados: "List<Estoque>",
                Workbook: "SXSSFWorkbook",
                Destino: "MemoryStream.ToArray()",
                Objetivo: "Isolar a economia obtida pelo workbook em streaming."),
            new(
                Rota: CenariosExportacao.SxssfStreamArquivo,
                FonteDeDados: "IEnumerable<Estoque>",
                Workbook: "SXSSFWorkbook",
                Destino: "Arquivo temporário e FileStream",
                Objetivo: "Eliminar a lista, o workbook integral e o arquivo final da memória."),
            new(
                Rota: CenariosExportacao.SxssfStreamResponse,
                FonteDeDados: "IEnumerable<Estoque>",
                Workbook: "SXSSFWorkbook",
                Destino: "Response.Body",
                Objetivo: "Transmitir o resultado diretamente ao cliente, sem arquivo final intermediário.")
        ];

        return Ok(cenarios);
    }

    [HttpGet(CenariosExportacao.Atual)]
    public IActionResult ExportarCenarioAtual(
        [FromQuery, Range(1, MaximoDeRegistros)] int quantidade = 100_000,
        CancellationToken cancellationToken = default)
    {
        var conteudo = exportService.ExportarCenarioAtual(quantidade, cancellationToken);

        return File(
            conteudo,
            ExcelContentType,
            CriarNomeArquivo(CenariosExportacao.Atual, quantidade));
    }

    [HttpGet(CenariosExportacao.XssfSemToArray)]
    public IActionResult ExportarXssfSemToArray(
        [FromQuery, Range(1, MaximoDeRegistros)] int quantidade = 100_000,
        CancellationToken cancellationToken = default)
    {
        var stream = exportService.ExportarXssfSemToArray(quantidade, cancellationToken);

        return File(
            stream,
            ExcelContentType,
            CriarNomeArquivo(CenariosExportacao.XssfSemToArray, quantidade));
    }

    [HttpGet(CenariosExportacao.SxssfComLista)]
    public IActionResult ExportarSxssfComLista(
        [FromQuery, Range(1, MaximoDeRegistros)] int quantidade = 100_000,
        CancellationToken cancellationToken = default)
    {
        var conteudo = exportService.ExportarSxssfComLista(quantidade, cancellationToken);

        return File(
            conteudo,
            ExcelContentType,
            CriarNomeArquivo(CenariosExportacao.SxssfComLista, quantidade));
    }

    [HttpGet(CenariosExportacao.SxssfStreamArquivo)]
    public IActionResult ExportarSxssfParaArquivoTemporario(
        [FromQuery, Range(1, MaximoDeRegistros)] int quantidade = 100_000,
        CancellationToken cancellationToken = default)
    {
        var caminho = exportService.ExportarSxssfParaArquivoTemporario(
            quantidade,
            cancellationToken);

        try
        {
            var stream = new FileStream(
                caminho,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan | FileOptions.DeleteOnClose);

            return File(
                stream,
                ExcelContentType,
                CriarNomeArquivo(CenariosExportacao.SxssfStreamArquivo, quantidade));
        }
        catch
        {
            System.IO.File.Delete(caminho);
            throw;
        }
    }

    [HttpGet(CenariosExportacao.SxssfStreamResponse)]
    public IActionResult ExportarSxssfDiretoParaResponse(
        [FromQuery, Range(1, MaximoDeRegistros)] int quantidade = 100_000,
        CancellationToken cancellationToken = default)
    {
        var bodyControlFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();

        if (bodyControlFeature is not null)
        {
            // O NPOI 2.7.6 expõe somente escrita síncrona do workbook.
            // A permissão fica restrita a este cenário experimental.
            bodyControlFeature.AllowSynchronousIO = true;
        }

        Response.ContentType = ExcelContentType;
        Response.Headers.ContentDisposition =
            $"attachment; filename=\"{CriarNomeArquivo(CenariosExportacao.SxssfStreamResponse, quantidade)}\"";

        exportService.ExportarSxssfParaStream(
            quantidade,
            Response.Body,
            cancellationToken);

        return new EmptyResult();
    }

    private static string CriarNomeArquivo(string cenario, int quantidade)
    {
        return $"estoque-{cenario}-{quantidade}.xlsx";
    }
}
