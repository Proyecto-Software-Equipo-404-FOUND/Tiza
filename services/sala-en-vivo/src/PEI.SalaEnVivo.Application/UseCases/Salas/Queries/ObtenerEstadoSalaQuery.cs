using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Salas;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Queries
{
    public record ObtenerEstadoSalaQuery(
        Guid UsuarioId,
        Guid InstitucionId,
        string Rol,
        Guid? SalaId = null) : IRequest<EstadoSalaDto>;
}
