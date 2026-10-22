namespace PEI.Identidad.Application.Exceptions;

// Resultado esperado al revalidar la identidad antes de persistir una sesión.
// No representa errores de conexión, configuración o ejecución de PostgreSQL.
public sealed class AuthenticationIdentityUnavailableException : Exception
{
    public AuthenticationIdentityUnavailableException() : base("La identidad ya no está disponible para autenticación.") { }
}
