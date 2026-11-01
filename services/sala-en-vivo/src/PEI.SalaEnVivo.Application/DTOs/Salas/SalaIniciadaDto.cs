namespace PEI.SalaEnVivo.Application.DTOs.Salas
{
    public sealed record SalaIniciadaDto(
        Guid SalaId,
        DateTime IniciadaUtc,
        int TotalPreguntas,
        DateTime FechaLimiteUtc);
}
