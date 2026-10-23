using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.Application.UseCases.Auth.Refresh;

public sealed class InstitutionalSessionService(LoginHandler login,
    IAuthenticationTokenWriter writer, IRefreshTokenService refreshTokens, IRefreshTokenHasher hashes, IAccessTokenIssuer accessTokens)
{
    public async Task<LoginResult> LoginAsync(LoginCommand command, CancellationToken ct = default)
    {
        var usuario = await login.AuthenticateAsync(command, ct);
        var refresh = refreshTokens.GenerateToken();
        var vence = DateTime.UtcNow.AddDays(7);
        try
        {
            return await writer.ConfirmarLoginAsync(usuario, hashes.Hash(refresh), vence,
                identidad => Emitir(identidad, refresh, vence), ct);
        }
        catch (AuthenticationIdentityUnavailableException) { throw new UnauthorizedAccessException("Credenciales invalidas."); }
    }

    public async Task<LoginResult> RefreshAsync(RefreshCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken) || command.RefreshToken.Length > 256) throw new UnauthorizedAccessException();
        var refresh = refreshTokens.GenerateToken();
        var vence = DateTime.UtcNow.AddDays(7);
        try
        {
            return await writer.ConfirmarRefreshAsync(hashes.Hash(command.RefreshToken), hashes.Hash(refresh), vence,
                identidad => Emitir(identidad, refresh, vence), ct);
        }
        catch (AuthenticationIdentityUnavailableException) { throw new UnauthorizedAccessException("Credenciales invalidas."); }
    }

    private LoginResult Emitir(RefreshIdentity identidad, string refresh, DateTime vence)
    {
        if (identidad.UsuarioId == Guid.Empty || identidad.InstitucionId == Guid.Empty || !IdentityRoles.IsCanonical(identidad.Rol))
            throw new AuthenticationIdentityUnavailableException();
        var access = accessTokens.Issue(new Usuario { Id = identidad.UsuarioId, InstitucionId = identidad.InstitucionId, Email = identidad.Email, Rol = identidad.Rol });
        return new(access.AccessToken, refresh, access.ExpiresAt, vence);
    }
}
