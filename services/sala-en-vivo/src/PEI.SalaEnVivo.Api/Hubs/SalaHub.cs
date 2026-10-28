using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MediatR;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using PEI.SalaEnVivo.Application.DTOs.Respuestas;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.UseCases.Salas.Commands;

namespace PEI.SalaEnVivo.Api.Hubs
{
    [Authorize]
    public sealed class SalaHub : Hub
    {
        private readonly ISender _sender;

        public SalaHub(ISender sender)
        {
            _sender = sender;
        }

        public async Task UnirseASala(int pin)
        {
            var user = Context.User;

            if (!Guid.TryParse(
                user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value,
                out var alumnoId))
            {
                throw new HubException("Alumno no identificado.");
            }

            var institucionClaim = user?.FindFirst("institucion_id")?.Value
                                   ?? user?.FindFirst("InstitucionId")?.Value;

            if (!Guid.TryParse(institucionClaim, out var institucionId))
            {
                throw new HubException("Institución no válida.");
            }

            try
            {
                var salaId = await _sender.Send(
                    new UnirseASalaPorPINCommand(
                        pin,
                        alumnoId,
                        institucionId),
                    Context.ConnectionAborted);

                Context.Items["SalaId"] = salaId;

                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    salaId.ToString(),
                    Context.ConnectionAborted);
            }
            catch (UnauthorizedAccessException)
            {
                throw new HubException(
                    "No tienes permiso para ingresar a esta sala.");
            }
            catch (InvalidOperationException ex)
            {
                throw new HubException(ex.Message);
            }
        }

        public async Task<ResultadoEnvioRespuestaDto> EnviarRespuesta(EnviarRespuestaRequest request)
        {
            var user = Context.User;

            if (!Guid.TryParse(
                user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value,
                out var alumnoId))
            {
                throw new HubException("Alumno no identificado.");
            }

            var institucionClaim = user?.FindFirst("institucion_id")?.Value
                                   ?? user?.FindFirst("InstitucionId")?.Value;

            _ = Guid.TryParse(institucionClaim, out var institucionId);

            // Obtener SalaId del contexto de la conexión o resolver desde base de datos
            if (!Context.Items.TryGetValue("SalaId", out var salaIdObj) || !(salaIdObj is Guid salaId))
            {
                var participanteRepo = Context.GetHttpContext()?.RequestServices.GetService<IParticipanteSalaRepository>();
                var salaIdRecuperada = participanteRepo != null
                    ? await participanteRepo.ObtenerUltimaSalaIdDeAlumnoAsync(alumnoId, Context.ConnectionAborted)
                    : null;

                if (!salaIdRecuperada.HasValue)
                {
                    return ResultadoEnvioRespuestaDto.Rechazada(
                        request.PreguntaId, request.IdempotencyKey, request.Secuencia,
                        "ALUMNO_NO_EN_SALA", "El alumno no se encuentra unido a ninguna sala activa.",
                        reintentable: false, correlationId: Context.ConnectionId);
                }

                salaId = salaIdRecuperada.Value;
                Context.Items["SalaId"] = salaId;
            }

            var command = new EnviarRespuestaCommand(
                salaId,
                alumnoId,
                institucionId,
                request.PreguntaId,
                request.Respuesta,
                request.IdempotencyKey,
                request.Secuencia,
                Context.ConnectionId);

            var resultado = await _sender.Send(command, Context.ConnectionAborted);

            // Si el envío fue aceptado, notificar en tiempo real al grupo de docentes
            if (resultado.Estado == "Aceptada")
            {
                await Clients.Group($"{salaId}:docentes").SendAsync(
                    "respuestaRegistrada",
                    new
                    {
                        salaId,
                        alumnoId,
                        preguntaId = request.PreguntaId,
                        recibidaUtc = resultado.RecibidaUtc
                    },
                    Context.ConnectionAborted);
            }

            return resultado;
        }
    }
}
