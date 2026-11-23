using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PEI.Identidad.Application.UseCases.Auth.Invitado;

namespace PEI.Identidad.Api.Controllers;

// S3-SA-01 (#231): ingreso de alumno invitado. Contrato: identidad-v1.json, POST /api/auth/invitado.
// Por el gateway: POST /api/v1/identidad/auth/invitado. Los errores llevan su código en `type`.
[ApiController, Route("api/auth")]
public sealed class InvitadosController(IngresoInvitadoHandler ingreso) : ControllerBase
{
    public sealed record IngresoInvitadoRequest(int Pin, string? Nombre);

    [AllowAnonymous, HttpPost("invitado"), EnableRateLimiting("login")]
    [ProducesResponseType(typeof(SesionInvitado), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ingresar(IngresoInvitadoRequest request, CancellationToken ct)
    {
        var respuesta = await ingreso.HandleAsync(new IngresoInvitadoCommand(request.Pin, request.Nombre), ct);

        if (respuesta.Resultado == ResultadoIngresoInvitado.Ok)
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";
            return Ok(respuesta.Sesion);
        }

        return respuesta.Resultado switch
        {
            ResultadoIngresoInvitado.NombreInvalido => Problem(statusCode: 400, type: "NOMBRE_INVALIDO",
                title: $"El nombre tiene que tener entre {IngresoInvitadoHandler.NombreMinimo} y {IngresoInvitadoHandler.NombreMaximo} caracteres."),
            ResultadoIngresoInvitado.SalaSinInvitados => Problem(statusCode: 403, type: "SALA_SIN_INVITADOS",
                title: "Esta sala no admite invitados. Pedile al docente que te habilite."),
            ResultadoIngresoInvitado.SalaFinalizada => Problem(statusCode: 409, type: "SALA_FINALIZADA",
                title: "La sala ya finalizó."),
            ResultadoIngresoInvitado.SalaNoDisponible => Problem(statusCode: 503, type: "SALA_NO_DISPONIBLE",
                title: "No pudimos verificar la sala. Probá de nuevo en unos segundos."),
            _ => Problem(statusCode: 404, type: "SALA_NO_ENCONTRADA", title: "Sala no encontrada.")
        };
    }
}
