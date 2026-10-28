using MediatR;
using PEI.SalaEnVivo.Application.DTOs.Respuestas;

namespace PEI.SalaEnVivo.Application.UseCases.Salas.Commands
{
    public record EnviarRespuestaCommand(
        Guid SalaId,
        Guid AlumnoId,
        Guid InstitucionId,
        Guid PreguntaId,
        string Respuesta,
        Guid IdempotencyKey,
        int Secuencia,
        string? CorrelationId = null) : IRequest<ResultadoEnvioRespuestaDto>;
}
