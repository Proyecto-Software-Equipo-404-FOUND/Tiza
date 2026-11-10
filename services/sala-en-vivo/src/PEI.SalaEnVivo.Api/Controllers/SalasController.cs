using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PEI.SalaEnVivo.Application.DTOs.Salas;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;
using PEI.SalaEnVivo.Application.UseCases.Salas.Handlers;

namespace PEI.SalaEnVivo.Api.Controllers
{
    [Authorize(Roles = "Docente,Administrador")]
    [ApiController]
    [Route("api/v1/salas")]
    public class SalasController : ControllerBase
    {
        private readonly CrearSalaCommandHandler _crearSalaCommandHandler;

        public SalasController(CrearSalaCommandHandler crearSalaCommandHandler)
        {
            _crearSalaCommandHandler = crearSalaCommandHandler;
        }

        [HttpPost]
        public async Task<IActionResult> CrearSala(
            [FromBody] CrearSalaRequestDto request,
            CancellationToken cancellationToken)
        {
            if (request == null || request.EvaluacionId == Guid.Empty)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "El identificador de evaluación es obligatorio.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            if (request.CapacidadMaxima < 1 || request.CapacidadMaxima > 100)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "La capacidad máxima debe estar entre 1 y 100.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var institucionClaim = User.FindFirst("institucion_id")?.Value
                ?? User.FindFirst("InstitucionId")?.Value;

            if (!Guid.TryParse(institucionClaim, out var institucionId))
            {
                return Unauthorized(new ProblemDetails
                {
                    Title = "El token no contiene un claim institucion_id válido.",
                    Status = StatusCodes.Status401Unauthorized
                });
            }

            var command = new CrearSalaCommand
            {
                InstitucionId = institucionId,
                EvaluacionId = request.EvaluacionId,
                CapacidadMaxima = request.CapacidadMaxima
            };

            try
            {
                var sala = await _crearSalaCommandHandler.HandleAsync(command, cancellationToken);

                return Created($"/api/v1/salas/{sala.Id}",
                    new
                    {
                        Id = sala.Id,
                        CodigoPIN = sala.CodigoPIN,
                        FechaVencimiento = sala.FechaVencimiento,
                        CapacidadMaxima = sala.CapacidadMaxima
                    });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ProblemDetails
                {
                    Title = ex.Message,
                    Status = StatusCodes.Status404NotFound
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Title = "No se pudo comunicar con el servicio de Evaluaciones.",
                    Status = StatusCodes.Status503ServiceUnavailable
                });
            }
        }
    }
}
