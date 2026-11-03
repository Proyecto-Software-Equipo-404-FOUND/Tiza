namespace PEI.SalaEnVivo.Application.DTOs.Presencia
{
    public sealed record PresenciaSalaDto(
        Guid SalaId,
        int TotalConectados,
        IReadOnlyList<AlumnoConectadoDto> Alumnos,
        DateTime ActualizadoUtc);
}
