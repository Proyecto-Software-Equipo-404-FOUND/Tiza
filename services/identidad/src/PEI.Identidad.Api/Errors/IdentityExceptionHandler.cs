using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PEI.Identidad.Application.Exceptions;
namespace PEI.Identidad.Api.Errors;
public sealed class IdentityExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            UnauthorizedAccessException => (401, "Credenciales invalidas."),
            RegistrationConflictException => (409, "No se pudo completar el registro por un conflicto de datos."),
            ValidationException or ArgumentException => (400, "Datos de solicitud invalidos."),
            _ => (0, "")
        };
        if (status == 0) return false;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: cancellationToken);
        return true;
    }
}
