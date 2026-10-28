namespace PEI.SalaEnVivo.Application.DTOs.Respuestas
{
    public record EnviarRespuestaRequest(
        Guid PreguntaId,
        string Respuesta,
        Guid IdempotencyKey,
        int Secuencia);
}
