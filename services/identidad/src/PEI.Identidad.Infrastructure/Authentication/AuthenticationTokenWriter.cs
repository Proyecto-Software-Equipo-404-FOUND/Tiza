using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
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

    public async Task CrearRefreshTokenAsync(Guid usuarioId, string tokenHash,
        DateTime fechaExpiracion, CancellationToken cancellationToken = default)
    {
        Validar(tokenHash, fechaExpiracion);
        if (usuarioId == Guid.Empty) throw new ArgumentException("Usuario invalido.");
        var filas = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RefreshToken" ("Id", "UsuarioId", "Token", "FechaExpiracion", "Revocado")
            SELECT {Guid.NewGuid()}, u."Id", {tokenHash}, {fechaExpiracion}, FALSE
            FROM "Usuario" u JOIN "Institucion" i ON i."Id" = u."InstitucionId"
            WHERE u."Id" = {usuarioId} AND u."Activo" AND i."Activo"
            FOR SHARE OF u, i
            """, cancellationToken);
        if (filas != 1) throw new AuthenticationIdentityUnavailableException();
    }

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
        Validar(tokenNuevoHash, fechaExpiracion);
        if (string.IsNullOrWhiteSpace(tokenAnteriorHash) || tokenAnteriorHash == tokenNuevoHash)
            throw new ArgumentException("Se requieren hashes distintos.");
        // Cada CTE materializado entrega filas solo después de adquirir sus locks.
        // La vigencia se comprueba sobre esa fila bloqueada con el reloj de PostgreSQL,
        // no sobre el target del UPDATE ni con una hora capturada antes de esperar.
        // Consumo y reemplazo permanecen en una única sentencia atómica.
        return await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            /* identidad:rotar-refresh */
            WITH propietario AS MATERIALIZED (
                SELECT u."Id" FROM "Usuario" u
                JOIN "Institucion" i ON i."Id" = u."InstitucionId"
                JOIN "RefreshToken" t ON t."UsuarioId" = u."Id"
                WHERE t."Token" = {tokenAnteriorHash} AND u."Activo" AND i."Activo"
                FOR SHARE OF u, i
            ), bloqueado AS MATERIALIZED (
                SELECT t."Id", t."UsuarioId", t."FechaExpiracion"
                FROM "RefreshToken" t
                JOIN propietario p ON p."Id" = t."UsuarioId"
                WHERE t."Token" = {tokenAnteriorHash} AND NOT t."Revocado"
                FOR UPDATE OF t
            ), consumido AS (
                UPDATE "RefreshToken" t SET "Revocado" = TRUE
                FROM bloqueado b
                WHERE t."Id" = b."Id" AND NOT t."Revocado"
                  AND b."FechaExpiracion" > clock_timestamp()
                RETURNING t."UsuarioId"
            )
            INSERT INTO "RefreshToken" ("Id", "UsuarioId", "Token", "FechaExpiracion", "Revocado")
            SELECT {Guid.NewGuid()}, "UsuarioId", {tokenNuevoHash}, {fechaExpiracion}, FALSE FROM consumido
            """, cancellationToken) == 1;
    }
}
