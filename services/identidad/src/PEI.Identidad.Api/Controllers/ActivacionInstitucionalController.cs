using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PEI.Identidad.Application.UseCases.Auth.Activation;

namespace PEI.Identidad.Api.Controllers;

[ApiController, Route("api/auth/invitaciones/alumnos")]
public sealed class ActivacionInstitucionalController(ActivarAlumnoHandler activar) : ControllerBase
{
    [AllowAnonymous, HttpPost("activar"), EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Activar(ActivarAlumnoCommand command, CancellationToken ct)
    {
        await activar.HandleAsync(command, ct);
        return NoContent();
    }
}
