using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PEI.Identidad.Application.Exceptions;

namespace PEI.Identidad.Api.Errors;

public sealed class TenantAccessDeniedHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not TenantAccessDeniedException) return false;
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Operacion no autorizada."
        }, cancellationToken);
        return true;
    }
}
