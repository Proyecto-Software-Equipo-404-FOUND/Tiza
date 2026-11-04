using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Salas;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public sealed record UnirseASalaDocenteCommand(
        Guid SalaId,
        Guid InstitucionId) : IRequest<ResultadoControlSala<Guid>>
    {
    }
}
