using MediatR;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public sealed record UnirseASalaPorPINCommand(
    int PIN,
    Guid AlumnoId,
    Guid InstitucionId,
    string? TokenAlumno = null
) : IRequest<Guid>;
}
