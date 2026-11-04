using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Salas;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public sealed record AvanzarPreguntaCommand(
        Guid SalaId,
        Guid InstitucionId,
        Guid? PreguntaId = null) : IRequest<ResultadoControlSala<PreguntaActualDto>>
    {
    }
}
