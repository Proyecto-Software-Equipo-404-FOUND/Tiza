using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MediatR;
using System.Security.Claims;
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
                user?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                out var alumnoId))
            {
                throw new HubException("Alumno no identificado.");
            }

            if (!Guid.TryParse(
                user?.FindFirst("InstitucionId")?.Value,
                out var institucionId))
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
    }
}
