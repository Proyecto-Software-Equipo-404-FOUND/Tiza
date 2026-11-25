using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Abstractions;

public interface IUsuariosModificacionDataAccess
{
    Task<Usuario?> ObtenerUsuarioAsync(Guid id, CancellationToken cancellationToken = default);
    Task DarDeBajaAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // S3-ID-03: guarda la nueva contraseña y revoca los refresh tokens del usuario, en una transacción
    Task CambiarContrasenaAsync(Guid usuarioId, string nuevoHash, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
