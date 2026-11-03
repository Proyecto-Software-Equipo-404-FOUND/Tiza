using PEI.Identidad.Application.UseCases.Usuarios;

namespace PEI.Identidad.Application.Abstractions;

// S2-ID-10: consultas de solo lectura sobre los usuarios de la institución del token.
public interface IUsuariosConsultaDataAccess
{
    Task<IReadOnlyList<UsuarioResumen>> ListarActivosAsync(string? rol, CancellationToken cancellationToken = default);
    Task<UsuarioResumen?> ObtenerActivoAsync(Guid id, CancellationToken cancellationToken = default);
}
