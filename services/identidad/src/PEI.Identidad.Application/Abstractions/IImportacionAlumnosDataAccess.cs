namespace PEI.Identidad.Application.Abstractions;

public enum ResultadoAltaAlumno
{
    Creado,
    Omitido
}

public sealed record ResultadoPersistenciaAlumno(string EmailNormalizado, ResultadoAltaAlumno Resultado);

public interface IImportacionAlumnosDataAccess
{
    Task<string> NormalizarEmailAsync(string email, CancellationToken ct);

    Task<ResultadoPersistenciaAlumno> CrearPendienteAsync(
        Guid administradorId,
        Guid institucionId,
        string nombre,
        string apellido,
        string email,
        CancellationToken ct);

    // S2-ID-02 (#135): alta directa con la contraseña inicial que definió el administrador.
    // El alumno queda activo y sin invitación. La implementación por defecto existe para que
    // los dobles de prueba que no usan esta opción sigan compilando.
    Task<ResultadoPersistenciaAlumno> CrearActivoAsync(
        Guid administradorId,
        Guid institucionId,
        string nombre,
        string apellido,
        string email,
        string passwordHash,
        CancellationToken ct) =>
        throw new NotSupportedException("Este acceso a datos no admite el alta con contraseña inicial.");
}
