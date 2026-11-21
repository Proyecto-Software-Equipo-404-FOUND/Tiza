namespace PEI.SalaEnVivo.Application.DTOs.Presencia
{
    public sealed record AlumnoConectadoDto(
        Guid AlumnoId,
        string Alias,
        DateTime IngresoUtc,
        bool EsInvitado = false,
        string? Nombre = null);
}
