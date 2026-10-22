using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.Infrastructure.Authentication;

public sealed class AuthenticationTokenWriter(IdentidadDbContext dbContext) : IAuthenticationTokenWriter
{
    private static void Validar(string hash, DateTime vencimiento)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length > 255 || hash.Any(char.IsWhiteSpace) ||
            vencimiento.Kind != DateTimeKind.Utc || vencimiento <= DateTime.UtcNow)
            throw new ArgumentException("Hash no vacio de hasta 255 caracteres sin espacios y vencimiento UTC futuro requeridos.");
    }

    public Task<T> ConfirmarLoginAsync<T>(Usuario autenticado, string tokenHash, DateTime vencimiento,
        Func<RefreshIdentity, T> emitir, CancellationToken cancellationToken = default) =>
        CrearSesionAsync(autenticado.Id, autenticado, tokenHash, vencimiento, emitir, cancellationToken);

    public async Task CrearRefreshTokenAsync(Guid usuarioId, string tokenHash,
        DateTime fechaExpiracion, CancellationToken cancellationToken = default) =>
        _ = await CrearSesionAsync(usuarioId, null, tokenHash, fechaExpiracion, _ => true, cancellationToken);

    private async Task<T> CrearSesionAsync<T>(Guid usuarioId, Usuario? autenticado, string tokenHash, DateTime vencimiento,
        Func<RefreshIdentity, T> emitir, CancellationToken ct)
    {
        Validar(tokenHash, vencimiento);
        if (usuarioId == Guid.Empty) throw new ArgumentException("Usuario invalido.");
        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        var usuario = await BloquearIdentidadAsync(usuarioId, ct);
        if (autenticado is not null && (autenticado.InstitucionId != usuario.InstitucionId ||
            autenticado.PasswordHash != usuario.PasswordHash || autenticado.Email != usuario.Email))
            throw new AuthenticationIdentityUnavailableException();
        var resultado = emitir(Identidad(usuario));
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RefreshToken" ("Id", "UsuarioId", "Token", "FechaExpiracion", "Revocado")
            VALUES ({Guid.NewGuid()}, {usuarioId}, {tokenHash}, {vencimiento}, FALSE)
            """, ct);
        await tx.CommitAsync(ct);
        return resultado;
    }

    // Todos los escritores de sesiones toman los locks en el mismo orden:
    // Institucion FOR SHARE -> Usuario FOR SHARE -> RefreshToken FOR UPDATE (solo refresh).
    // La lectura inicial unicamente localiza filas; los datos para claims se releen tras los locks.
    private async Task<Usuario> BloquearIdentidadAsync(Guid usuarioId, CancellationToken ct)
    {
        var institucion = await dbContext.Usuarios.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == usuarioId).Select(u => u.InstitucionId).SingleOrDefaultAsync(ct);
        if (institucion == Guid.Empty) throw new AuthenticationIdentityUnavailableException();
        var instituciones = await dbContext.Database.SqlQuery<Guid>($"""
            /* identidad:rotar-refresh identidad:crear-sesion */
            SELECT "Id" AS "Value" FROM "Institucion" WHERE "Id" = {institucion} AND "Activo" FOR SHARE
            """).ToListAsync(ct);
        if (instituciones.Count != 1) throw new AuthenticationIdentityUnavailableException();
        var usuarios = await dbContext.Database.SqlQuery<Guid>($"""
            /* identidad:rotar-refresh identidad:crear-sesion */
            SELECT "Id" AS "Value" FROM "Usuario"
            WHERE "Id" = {usuarioId} AND "InstitucionId" = {institucion} AND "Activo" FOR SHARE
            """).ToListAsync(ct);
        if (usuarios.Count != 1) throw new AuthenticationIdentityUnavailableException();
        var usuario = await dbContext.Usuarios.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.Id == usuarioId, ct);
        if (!usuario.Activo || !IdentityRoles.IsCanonical(usuario.Rol)) throw new AuthenticationIdentityUnavailableException();
        return usuario;
    }

    private static RefreshIdentity Identidad(Usuario usuario) => new(usuario.Id, usuario.InstitucionId, usuario.Email, usuario.Rol);

    public async Task<bool> RevocarRefreshTokenAsync(string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return false;
        return await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            /* identidad:revocar-refresh */
            UPDATE "RefreshToken" SET "Revocado" = TRUE
            WHERE "Token" = {tokenHash} AND NOT "Revocado"
            """, cancellationToken) == 1;
    }

    public async Task<bool> RotarRefreshTokenAsync(string tokenAnteriorHash, string tokenNuevoHash,
        DateTime fechaExpiracion, CancellationToken cancellationToken = default)
    {
        try { return await ConfirmarRefreshAsync(tokenAnteriorHash, tokenNuevoHash, fechaExpiracion, _ => true, cancellationToken); }
        catch (AuthenticationIdentityUnavailableException) { return false; }
    }

    public async Task<T> ConfirmarRefreshAsync<T>(string anteriorHash, string nuevoHash, DateTime vencimiento,
        Func<RefreshIdentity, T> emitir, CancellationToken cancellationToken = default)
    {
        Validar(nuevoHash, vencimiento);
        if (string.IsNullOrWhiteSpace(anteriorHash) || anteriorHash == nuevoHash)
            throw new ArgumentException("Se requieren hashes distintos.");
        var ct = cancellationToken;
        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
        var usuarioId = await dbContext.RefreshTokens.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Token == anteriorHash).Select(t => t.UsuarioId).SingleOrDefaultAsync(ct);
        if (usuarioId == Guid.Empty) throw new AuthenticationIdentityUnavailableException();
        var usuario = await BloquearIdentidadAsync(usuarioId, ct);
        var tokens = await dbContext.Database.SqlQuery<Guid>($"""
            /* identidad:rotar-refresh */
            SELECT "Id" AS "Value" FROM "RefreshToken"
            WHERE "Token" = {anteriorHash} AND "UsuarioId" = {usuarioId} AND NOT "Revocado" FOR UPDATE
            """).ToListAsync(ct);
        if (tokens.Count != 1) throw new AuthenticationIdentityUnavailableException();
        var tokenId = tokens[0];
        // Sentencia posterior a la espera: nunca se evalua vigencia sobre una fila aun sin lock.
        var vigentes = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM "RefreshToken"
            WHERE "Id" = {tokenId} AND NOT "Revocado" AND "FechaExpiracion" > clock_timestamp()
            """).ToListAsync(ct);
        if (vigentes.Count != 1) throw new AuthenticationIdentityUnavailableException();
        var resultado = emitir(Identidad(usuario));
        var filas = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            /* identidad:rotar-refresh */
            WITH consumido AS (
                UPDATE "RefreshToken" SET "Revocado" = TRUE
                WHERE "Id" = {tokenId} AND "UsuarioId" = {usuarioId} AND NOT "Revocado"
                  AND "FechaExpiracion" > clock_timestamp()
                RETURNING "UsuarioId"
            )
            INSERT INTO "RefreshToken" ("Id", "UsuarioId", "Token", "FechaExpiracion", "Revocado")
            SELECT {Guid.NewGuid()}, "UsuarioId", {nuevoHash}, {vencimiento}, FALSE FROM consumido
            """, ct);
        if (filas != 1) throw new AuthenticationIdentityUnavailableException();
        await tx.CommitAsync(ct);
        return resultado;
    }
}
