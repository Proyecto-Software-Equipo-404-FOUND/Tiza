using MediatR;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public sealed record UnirseASalaPorPINCommand(
    int PIN,
    Guid AlumnoId,
    Guid InstitucionId,
    string? TokenAlumno = null,
    // S3-SA-01 (#231): ingreso de un invitado (token de rol Invitado emitido por Identidad).
    bool EsInvitado = false,
    Guid? SalaIdInvitado = null,
    string? NombreInvitado = null
) : IRequest<Guid>;
}
