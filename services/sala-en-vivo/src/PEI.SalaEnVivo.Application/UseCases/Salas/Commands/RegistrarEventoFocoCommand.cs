using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Foco;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public record RegistrarEventoFocoCommand(
        Guid SalaId,
        Guid AlumnoId,
        Guid InstitucionId,
        int DuracionMs,
        string? TipoEvento = null,
        Guid? PreguntaId = null) : IRequest<EventoFocoRegistradoDto>;
}
