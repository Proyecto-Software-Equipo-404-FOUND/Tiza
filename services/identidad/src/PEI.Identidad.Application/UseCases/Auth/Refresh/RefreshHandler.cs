using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.UseCases.Auth.Refresh;

public class RefreshHandler
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly ITokenService _tokenService;

    public RefreshHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenHasher refreshTokenHasher,
        ITokenService tokenService)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResult> HandleAsync(RefreshCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            throw new UnauthorizedAccessException(
                "Refresh token inválido.");
        }

        var tokenHash = _refreshTokenHasher.Hash(command.RefreshToken);
        var tokenActual =
            await _refreshTokenRepository.GetByTokenAsync(tokenHash);

        if (tokenActual is null ||
            tokenActual.Revocado ||
            tokenActual.FechaExpiracion <= DateTime.UtcNow ||
            !tokenActual.Usuario.Activo ||
            !tokenActual.Usuario.Institucion.Activo)
        {
            throw new UnauthorizedAccessException(
                "Refresh token inválido o vencido.");
        }

        var ahora = DateTime.UtcNow;
        var accessToken = _tokenService.GenerateToken(tokenActual.Usuario);
        var nuevoRefreshToken = _refreshTokenService.GenerateToken();

        var accessTokenExpiresAt = ahora.AddMinutes(60);
        var refreshTokenExpiresAt = ahora.AddDays(7);

        // Revocar el refresh token anterior.
        tokenActual.Revocado = true;

        // Guardar solamente el hash del nuevo refresh token.
        var nuevoToken = new RefreshToken
        {
            UsuarioId = tokenActual.UsuarioId,
            Token = _refreshTokenHasher.Hash(nuevoRefreshToken),
            FechaExpiracion = refreshTokenExpiresAt,
            Revocado = false
        };

        await _refreshTokenRepository.AddAsync(nuevoToken);

        // Ambas modificaciones se guardan en una misma operación.
        await _refreshTokenRepository.SaveChangesAsync();

        return new LoginResult(
            accessToken,
            nuevoRefreshToken,
            accessTokenExpiresAt,
            refreshTokenExpiresAt);
    }
}