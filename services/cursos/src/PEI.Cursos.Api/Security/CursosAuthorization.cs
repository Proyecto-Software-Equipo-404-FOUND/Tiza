namespace PEI.Cursos.Api.Security;

internal static class CursosAuthorization
{
    internal const string Administrador = "Administrador";
    internal const string Docente = "Docente";
    internal const string SoloAdministrador = "CursosSoloAdministrador";
    internal const string AdministradorODocente = "CursosAdministradorODocente";
    internal const string Alumno = "Alumno";
    // Admin y docente consultan a cualquiera; el alumno, solo a sí mismo (lo verifica la acción con el claim sub)
    internal const string GestionOAlumnoPropio = "CursosGestionOAlumnoPropio";
}
