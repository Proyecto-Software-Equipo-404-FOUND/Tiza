using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Abstractions;

public interface IUsuariosModificacionDataAccess
{
    Task<Usuario?> ObtenerUsuarioAsync(Guid id, CancellationToken cancellationToken = default);
    Task DarDeBajaAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
