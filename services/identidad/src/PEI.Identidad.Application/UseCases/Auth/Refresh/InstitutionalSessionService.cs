using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.Application.UseCases.Auth.Refresh;

public sealed class InstitutionalSessionService(LoginHandler login, IAuthenticationDataAccess data,
    IAuthenticationTokenWriter writer, IRefreshTokenService refreshTokens, IRefreshTokenHasher hashes, IAccessTokenIssuer accessTokens)
{
    public async Task<LoginResult> LoginAsync(LoginCommand command, CancellationToken ct = default)
    {
        var usuario = await login.AuthenticateAsync(command, ct);
        var resultado = Emitir(usuario);
        try
        {
            await writer.CrearRefreshTokenAsync(usuario.Id, hashes.Hash(resultado.RefreshToken), resultado.RefreshTokenExpiresAt, ct);
        }
        catch (AuthenticationIdentityUnavailableException) { throw new UnauthorizedAccessException("Credenciales invalidas."); }
        return resultado;
    }

    public async Task<LoginResult> RefreshAsync(RefreshCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken) || command.RefreshToken.Length > 256) throw new UnauthorizedAccessException();
        var hash = hashes.Hash(command.RefreshToken);
        var identidad = await data.BuscarIdentidadParaRenovacionAsync(hash, ct);
        if (identidad is null || identidad.UsuarioId == Guid.Empty || identidad.InstitucionId == Guid.Empty || !IdentityRoles.IsCanonical(identidad.Rol))
            throw new UnauthorizedAccessException();
        var resultado = Emitir(new Usuario { Id = identidad.UsuarioId, InstitucionId = identidad.InstitucionId, Email = identidad.Email, Rol = identidad.Rol });
        // La escritura condicional de ID-05 arbitra reutilización y concurrencia en PostgreSQL.
        if (!await writer.RotarRefreshTokenAsync(hash, hashes.Hash(resultado.RefreshToken), resultado.RefreshTokenExpiresAt, ct))
            throw new UnauthorizedAccessException();
        return resultado;
    }

    private LoginResult Emitir(Usuario usuario)
    {
        var access = accessTokens.Issue(usuario);
        return new(access.AccessToken, refreshTokens.GenerateToken(), access.ExpiresAt, DateTime.UtcNow.AddDays(7));
    }
}
