using PEI.Identidad.Application.UseCases.Usuarios;

namespace PEI.Identidad.Application.Abstractions;

// S2-ID-10: consultas de solo lectura sobre los usuarios de la institución del token.
public interface IUsuariosConsultaDataAccess
{
    Task<IReadOnlyList<UsuarioResumen>> ListarActivosAsync(string? rol, CancellationToken cancellationToken = default);

    // S3-ID-01: la pantalla Usuarios también muestra a los dados de baja para poder reactivarlos.
    Task<IReadOnlyList<UsuarioResumen>> ListarIncluyendoInactivosAsync(string? rol, CancellationToken cancellationToken = default) =>
        ListarActivosAsync(rol, cancellationToken);
    Task<UsuarioResumen?> ObtenerActivoAsync(Guid id, CancellationToken cancellationToken = default);
}
