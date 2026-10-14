using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PEI.BuildingBlocks.Logging;

namespace PEI.BuildingBlocks.Errors;

public class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no controlada capturada por ProblemDetailsMiddleware");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.CorrelationIdHeader]?.ToString()
                            ?? Guid.NewGuid().ToString();

        var statusCode = exception switch
        {
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            InvalidOperationException => (int)HttpStatusCode.Conflict,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = exception switch
            {
                KeyNotFoundException => "Recurso no encontrado",
                UnauthorizedAccessException => "Acceso no autorizado",
                InvalidOperationException => "Conflicto en la operación",
                ArgumentException => "Petición inválida",
                _ => "Ocurrió un error inesperado"
            },
            Detail = exception.Message,
            Instance = context.Request.Path
        };

        problem.Extensions["correlationId"] = correlationId;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
