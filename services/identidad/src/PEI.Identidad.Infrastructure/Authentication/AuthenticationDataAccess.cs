using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.Infrastructure.Authentication;

public sealed class AuthenticationDataAccess : IAuthenticationDataAccess
{
    private readonly IdentidadDbContext _dbContext;

    public AuthenticationDataAccess(IdentidadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Usuario?> BuscarUsuarioParaLoginAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _dbContext.Usuarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                usuario => EF.Property<string>(usuario, "EmailNormalizado") == EmailDbFunctions.Normalize(email)
                    && usuario.Activo
                    && usuario.Institucion.Activo,
                cancellationToken);
    }

    public async Task<RefreshToken?> BuscarRefreshTokenAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                token => token.Token == tokenHash
                    && !token.Revocado
                    && token.FechaExpiracion > DateTime.UtcNow
                    && token.Usuario.Activo
                    && token.Usuario.Institucion.Activo,
                cancellationToken);
    }
    public Task<RefreshIdentity?> BuscarIdentidadParaRenovacionAsync(string tokenHash,
        CancellationToken cancellationToken = default) =>
        _dbContext.RefreshTokens.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Token == tokenHash && !t.Revocado && t.FechaExpiracion > DateTime.UtcNow
                && t.Usuario.Activo && t.Usuario.Institucion.Activo)
            .Select(t => new RefreshIdentity(t.UsuarioId, t.Usuario.InstitucionId,
                t.Usuario.Email, t.Usuario.Rol))
            .SingleOrDefaultAsync(cancellationToken);
}
