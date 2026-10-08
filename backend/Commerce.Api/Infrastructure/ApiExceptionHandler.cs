using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Infrastructure;

/// <summary>Traduce excepciones a respuestas ProblemDetails (400 para validacion, 500 para el resto).</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex is CsvValidationException
            ? (StatusCodes.Status400BadRequest, ex.Message)
            : (StatusCodes.Status500InternalServerError, "Error interno del servidor.");

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Error no controlado");

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title }, ct);
        return true;
    }
}
