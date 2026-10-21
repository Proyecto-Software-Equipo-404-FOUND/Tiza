using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;

namespace PEI.Identidad.Application.UseCases.Auth.Refresh;

public sealed class LogoutHandler(IAuthenticationTokenWriter writer, IRefreshTokenHasher hashes)
{
    public async Task HandleAsync(LogoutCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken) || command.RefreshToken.Length > 256) return;
        // Capacidad basada en posesión del secreto: no requiere JWT vigente ni tenant.
        // El escritor puede revocar también tokens de usuarios/instituciones inactivos.
        await writer.RevocarRefreshTokenAsync(hashes.Hash(command.RefreshToken), ct);
    }
}
