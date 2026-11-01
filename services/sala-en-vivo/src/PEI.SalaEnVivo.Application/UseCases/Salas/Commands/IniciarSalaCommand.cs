using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Salas;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public sealed record IniciarSalaCommand(
        Guid SalaId,
        Guid InstitucionId) : IRequest<ResultadoControlSala<ResultadoInicioSalaDto>>
    {
    }
}
