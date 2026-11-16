using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Salas;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public sealed record FinalizarSalaCommand(
        Guid SalaId,
        Guid InstitucionId,
        string? Motivo = null) : IRequest<ResultadoControlSala<FinalizacionSalaDto>>
    {
    }
}
