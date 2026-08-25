using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using OutOfMemoryWorkbook.Models;
using OutOfMemoryWorkbook.Services;

namespace OutOfMemoryWorkbook.Controllers;

[ApiController]
[Route("api/exportacoes/estoque")]
public sealed class InventoryExportsController(
    IInventoryExportService exportService) : ControllerBase
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const int MaximumRecords = 1_048_575;

    [HttpGet("cenarios")]
    public ActionResult<IReadOnlyCollection<ExportScenario>> GetScenarios()
    {
        ExportScenario[] scenarios =
        [
            new(
                Route: ExportScenarios.Current,
                DataSource: "List<Estoque>",
                Workbook: "XSSFWorkbook",
                Target: "MemoryStream.ToArray()",
                Objective: "Reproduzir o maior consumo de memória do fluxo atual."),
            new(
                Route: ExportScenarios.XssfWithoutToArray,
                DataSource: "List<Estoque>",
                Workbook: "XSSFWorkbook",
                Target: "MemoryStream retornado como Stream",
                Objective: "Isolar apenas o custo da cópia criada pelo ToArray()."),
            new(
                Route: ExportScenarios.SxssfWithList,
                DataSource: "List<Estoque>",
                Workbook: "SXSSFWorkbook",
                Target: "MemoryStream.ToArray()",
                Objective: "Isolar a economia obtida pelo workbook em streaming."),
            new(
                Route: ExportScenarios.SxssfFileStream,
                DataSource: "IEnumerable<Estoque>",
                Workbook: "SXSSFWorkbook",
                Target: "Arquivo temporário e FileStream",
                Objective: "Eliminar a lista, o workbook integral e o arquivo final da memória."),
            new(
                Route: ExportScenarios.SxssfResponseStream,
                DataSource: "IEnumerable<Estoque>",
                Workbook: "SXSSFWorkbook",
                Target: "Response.Body",
                Objective: "Transmitir o resultado diretamente ao cliente, sem arquivo final intermediário.")
        ];

        return Ok(scenarios);
    }

    [HttpGet(ExportScenarios.Current)]
    public IActionResult ExportCurrentScenario(
        [FromQuery(Name = "quantidade"), Range(1, MaximumRecords)] int quantity = 100_000,
        CancellationToken cancellationToken = default)
    {
        var content = exportService.ExportCurrentScenario(quantity, cancellationToken);

        return File(
            content,
            ExcelContentType,
            CreateFileName(ExportScenarios.Current, quantity));
    }

    [HttpGet(ExportScenarios.XssfWithoutToArray)]
    public IActionResult ExportXssfWithoutToArray(
        [FromQuery(Name = "quantidade"), Range(1, MaximumRecords)] int quantity = 100_000,
        CancellationToken cancellationToken = default)
    {
        var stream = exportService.ExportXssfWithoutToArray(quantity, cancellationToken);

        return File(
            stream,
            ExcelContentType,
            CreateFileName(ExportScenarios.XssfWithoutToArray, quantity));
    }

    [HttpGet(ExportScenarios.SxssfWithList)]
    public IActionResult ExportSxssfWithList(
        [FromQuery(Name = "quantidade"), Range(1, MaximumRecords)] int quantity = 100_000,
        CancellationToken cancellationToken = default)
    {
        var content = exportService.ExportSxssfWithList(quantity, cancellationToken);

        return File(
            content,
            ExcelContentType,
            CreateFileName(ExportScenarios.SxssfWithList, quantity));
    }

    [HttpGet(ExportScenarios.SxssfFileStream)]
    public IActionResult ExportSxssfToTemporaryFile(
        [FromQuery(Name = "quantidade"), Range(1, MaximumRecords)] int quantity = 100_000,
        CancellationToken cancellationToken = default)
    {
        var path = exportService.ExportSxssfToTemporaryFile(
            quantity,
            cancellationToken);

        try
        {
            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan | FileOptions.DeleteOnClose);

            return File(
                stream,
                ExcelContentType,
                CreateFileName(ExportScenarios.SxssfFileStream, quantity));
        }
        catch
        {
            System.IO.File.Delete(path);
            throw;
        }
    }

    [HttpGet(ExportScenarios.SxssfResponseStream)]
    public IActionResult ExportSxssfDirectlyToResponse(
        [FromQuery(Name = "quantidade"), Range(1, MaximumRecords)] int quantity = 100_000,
        CancellationToken cancellationToken = default)
    {
        var bodyControlFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();

        if (bodyControlFeature is not null)
        {
            // NPOI 2.7.6 only exposes synchronous workbook writing.
            // Keep this permission scoped to the experimental scenario.
            bodyControlFeature.AllowSynchronousIO = true;
        }

        Response.ContentType = ExcelContentType;
        Response.Headers.ContentDisposition =
            $"attachment; filename=\"{CreateFileName(ExportScenarios.SxssfResponseStream, quantity)}\"";

        exportService.ExportSxssfToStream(
            quantity,
            Response.Body,
            cancellationToken);

        return new EmptyResult();
    }

    private static string CreateFileName(string scenario, int quantity)
    {
        return $"estoque-{scenario}-{quantity}.xlsx";
    }
}
