namespace PEI.SalaEnVivo.Application.DTOs.Presencia
{
    public sealed record RegistroPresenciaDto(
        AlumnoConectadoDto Alumno,
        string? ConexionAnterior,
        bool EsNuevo,
        int TotalConectados);
}
