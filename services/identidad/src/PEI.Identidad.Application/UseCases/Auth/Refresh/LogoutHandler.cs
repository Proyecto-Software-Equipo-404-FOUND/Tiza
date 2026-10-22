using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Application.Interfaces.Security;

namespace PEI.Identidad.Application.UseCases.Auth.Refresh;

public class LogoutHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenHasher _refreshTokenHasher;

    public LogoutHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenHasher refreshTokenHasher)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenHasher = refreshTokenHasher;
    }

    public async Task HandleAsync(LogoutCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return;
        }

        var tokenHash = _refreshTokenHasher.Hash(command.RefreshToken);
        var token =
            await _refreshTokenRepository.GetByTokenAsync(tokenHash);

        // El cierre de sesión es idempotente: si no existe,
        // ya fue revocado o expiró, no hay nada que revocar.
        if (token is null || token.Revocado)
        {
            return;
        }

        token.Revocado = true;
        await _refreshTokenRepository.SaveChangesAsync();
    }
}