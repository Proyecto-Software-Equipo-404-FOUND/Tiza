using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.Abstractions;

public interface IAuthenticationDataAccess
{
    Task<RefreshIdentity?> BuscarIdentidadParaRenovacionAsync(string tokenHash,
        CancellationToken cancellationToken = default);

    Task<Usuario?> BuscarUsuarioParaLoginAsync(
        string email,
        CancellationToken cancellationToken = default);
}
