namespace PEI.SalaEnVivo.Application.DTOs.Presencia
{
    public sealed record ResultadoDesconexionPresenciaDto(
        bool AlumnoEliminado,
        AlumnoConectadoDto? Alumno,
        int TotalConectados);
}
