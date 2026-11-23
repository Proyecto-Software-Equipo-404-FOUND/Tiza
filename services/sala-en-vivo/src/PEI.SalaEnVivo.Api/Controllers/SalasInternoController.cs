using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;

namespace PEI.SalaEnVivo.Api.Controllers
{
    /// <summary>
    /// S3-SA-01 (#231): Identidad consulta la sala por PIN antes de emitir el token de invitado
    /// (contrato sala-en-vivo-v1.json, GET /interno/salas/pin/{pin}). Endpoint interno entre
    /// servicios: se autentica con X-Api-Key-Interna (RN-INT-01) y no se publica en el gateway.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("interno/salas")]
    public sealed class SalasInternoController : ControllerBase
    {
        private const string EncabezadoClave = "X-Api-Key-Interna";

        private readonly ISalaRepository _salaRepository;
        private readonly IConfiguration _configuracion;

        public SalasInternoController(ISalaRepository salaRepository, IConfiguration configuracion)
        {
            _salaRepository = salaRepository;
            _configuracion = configuracion;
        }

        [HttpGet("pin/{pin:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ObtenerParaInvitado(
            int pin,
            [FromHeader(Name = EncabezadoClave)] string? apiKey,
            CancellationToken cancellationToken)
        {
            if (!ClaveValida(apiKey))
            {
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Credencial interna inválida.");
            }

            var sala = await _salaRepository.ObtenerActivaPorPINAsync(pin, cancellationToken);
            if (sala == null)
            {
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Sala no encontrada.",
                    type: "SALA_NO_ENCONTRADA");
            }

            return Ok(new
            {
                salaId = sala.Id,
                institucionId = sala.InstitucionId,
                permiteInvitados = sala.PermitirInvitados,
                // Solo se encuentran por PIN las salas abiertas a nuevos ingresos.
                estado = "Abierta"
            });
        }

        private bool ClaveValida(string? recibida)
        {
            var esperada = _configuracion["ServiciosInternos:ApiKey"]
                ?? Environment.GetEnvironmentVariable("ServiciosInternos__ApiKey");

            if (string.IsNullOrEmpty(esperada) || string.IsNullOrEmpty(recibida))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(esperada),
                Encoding.UTF8.GetBytes(recibida));
        }
    }
}
