namespace PEI.SalaEnVivo.Application.DTOs.Salas
{
    public sealed record PreguntaActualDto(
        Guid SalaId,
        Guid PreguntaId,
        int Indice,
        int TotalPreguntas,
        string Tipo,
        string Enunciado,
        IReadOnlyList<OpcionPreguntaActualDto>? Opciones,
        decimal PuntajeMaximo,
        DateTime FechaInicioUtc,
        DateTime FechaLimiteUtc);
}
