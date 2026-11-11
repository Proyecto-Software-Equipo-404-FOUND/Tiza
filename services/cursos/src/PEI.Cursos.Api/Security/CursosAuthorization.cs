namespace PEI.Cursos.Api.Security;

internal static class CursosAuthorization
{
    internal const string Administrador = "Administrador";
    internal const string Docente = "Docente";
    internal const string SoloAdministrador = "CursosSoloAdministrador";
    internal const string AdministradorODocente = "CursosAdministradorODocente";
}
