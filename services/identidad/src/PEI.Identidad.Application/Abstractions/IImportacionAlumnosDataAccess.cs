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
}
