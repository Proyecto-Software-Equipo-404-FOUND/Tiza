namespace PEI.SalaEnVivo.Application.DTOs.Salas
{
    public record EstadoSalaDto(
        Guid SalaId,
        string Estado,
        PreguntaActualDto? PreguntaActual,
        int UltimaSecuencia,
        IReadOnlyList<Guid> PreguntasRespondidas,
        DateTime ServidorUtc,
        DateTime? FechaLimiteUtc);

    public record PreguntaActualDto(
        Guid SalaId,
        Guid PreguntaId,
        int Indice,
        int TotalPreguntas,
        string Tipo,
        string Enunciado,
        IReadOnlyList<OpcionPreguntaDto>? Opciones,
        decimal PuntajeMaximo,
        DateTime FechaInicioUtc,
        DateTime FechaLimiteUtc);

    public record OpcionPreguntaDto(
        Guid Id,
        string Texto);
}
