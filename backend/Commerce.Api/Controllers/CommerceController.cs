using Commerce.Api.Infrastructure;
using Commerce.Api.Models;
using Commerce.Api.Repositories;
using Commerce.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CommerceController(
    ICommerceCsvParser parser,
    ICommerceRepository repository,
    ILogger<CommerceController> logger) : ControllerBase
{
    private const int MaxPageSize = 200;

    /// <summary>Carga el archivo commerce_DDMMYYYY.csv: sp_create_commerce crea las tablas (si no existen) con las columnas del CSV y registra las filas.</summary>
    /// <response code="400">Archivo vacio, sin registros, nombre o formato invalido.</response>
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(UploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadResult>> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "El archivo esta vacio." });

        await using var stream = file.OpenReadStream();
        var csv = parser.Parse(file.FileName, stream);   // CsvValidationException -> 400 (ver middleware)
        var inserted = await repository.CreateAsync(csv, ct);

        logger.LogInformation("Archivo {File} cargado: {Rows} registros", file.FileName, inserted);
        return Ok(new UploadResult(file.FileName, inserted));
    }

    /// <summary>Procesa los registros de una fecha (pc_processdate) con sp_process_commerce.
    /// Devuelve la cantidad de registros enviados a commerce_quarantine.</summary>
    [HttpPost("process")]
    [ProducesResponseType(typeof(ProcessResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProcessResult>> Process([FromBody] ProcessRequest request, CancellationToken ct)
    {
        var quarantined = await repository.ProcessAsync(request.ProcessDate, ct);
        logger.LogInformation("Procesada fecha {Date}: {Count} a cuarentena", request.ProcessDate, quarantined);
        return Ok(new ProcessResult(request.ProcessDate, quarantined));
    }

    /// <summary>Lista commerce_quarantine (con paginacion y filtro opcional por fecha).</summary>
    [HttpGet("quarantine")]
    [ProducesResponseType(typeof(QuarantinePage), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuarantinePage>> GetQuarantine(
        [FromQuery] DateOnly? processDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        return Ok(await repository.GetQuarantineAsync(processDate, page, pageSize, ct));
    }

    /// <summary>Fechas con registros cargados (pendientes de procesar), para el selector del front.</summary>
    [HttpGet("process-dates")]
    public async Task<ActionResult<IReadOnlyList<PendingDate>>> GetProcessDates(CancellationToken ct) =>
        Ok(await repository.GetPendingDatesAsync(ct));
}
