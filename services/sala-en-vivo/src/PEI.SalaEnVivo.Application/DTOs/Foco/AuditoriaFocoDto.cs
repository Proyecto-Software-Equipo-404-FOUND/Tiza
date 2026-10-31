namespace PEI.SalaEnVivo.Application.DTOs.Foco
{
    public record RegistrarEventoFocoRequest(
        int DuracionMs,
        string? TipoEvento = null,
        Guid? PreguntaId = null);

    public record EventoFocoRegistradoDto(
        Guid Id,
        Guid SalaId,
        Guid AlumnoId,
        Guid? PreguntaId,
        string TipoEvento,
        int DuracionMs,
        string Severidad,
        DateTime RegistradoUtc);
}
