namespace PEI.Identidad.Domain.Security;
public static class IdentityRoles
{
    public const string Administrador = "Administrador";
    public const string Docente = "Docente";
    public const string Alumno = "Alumno";
    public static bool IsCanonical(string role) => role is Administrador or Docente or Alumno;
    public static bool IsAssignable(string role) => role is Docente or Alumno;
}
