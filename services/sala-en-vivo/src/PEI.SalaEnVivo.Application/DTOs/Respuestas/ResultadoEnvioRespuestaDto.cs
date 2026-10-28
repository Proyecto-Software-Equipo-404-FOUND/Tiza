namespace PEI.SalaEnVivo.Application.DTOs.Respuestas
{
    public record ResultadoEnvioRespuestaDto(
        string Estado,
        Guid? PreguntaId,
        Guid? IdempotencyKey,
        int? Secuencia,
        DateTime RecibidaUtc,
        ErrorHubDto? Error)
    {
        public static ResultadoEnvioRespuestaDto Aceptada(
            Guid preguntaId,
            Guid idempotencyKey,
            int secuencia,
            DateTime recibidaUtc) =>
            new("Aceptada", preguntaId, idempotencyKey, secuencia, recibidaUtc, null);

        public static ResultadoEnvioRespuestaDto Duplicada(
            Guid preguntaId,
            Guid idempotencyKey,
            int secuencia,
            DateTime recibidaUtcOriginal) =>
            new("Duplicada", preguntaId, idempotencyKey, secuencia, recibidaUtcOriginal, null);

        public static ResultadoEnvioRespuestaDto Rechazada(
            Guid? preguntaId,
            Guid? idempotencyKey,
            int? secuencia,
            string codigo,
            string mensaje,
            bool reintentable = false,
            string? correlationId = null,
            object? detalles = null) =>
            new("Rechazada", preguntaId, idempotencyKey, secuencia, DateTime.UtcNow,
                new ErrorHubDto(codigo, mensaje, reintentable, correlationId, detalles));
    }

    public record ErrorHubDto(
        string Codigo,
        string Mensaje,
        bool Reintentable,
        string? CorrelationId = null,
        object? Detalles = null);
}
