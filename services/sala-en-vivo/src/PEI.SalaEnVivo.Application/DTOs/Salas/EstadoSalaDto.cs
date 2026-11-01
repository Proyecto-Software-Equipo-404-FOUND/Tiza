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
}
