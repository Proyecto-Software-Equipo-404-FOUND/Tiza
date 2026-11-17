using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.Infrastructure.Plataforma;

public sealed class ActivacionAlumnoDataAccess(PlataformaDbContext db) : IActivacionAlumnoDataAccess
{
    public async Task ActivarAsync(string token, string passwordHash, CancellationToken ct)
    {
        if (token.Length != 43 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new UnauthorizedAccessException();

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT v."UsuarioId" AS "Value" FROM "InvitacionUsuario" v
            JOIN "Usuario" u ON u."Id" = v."UsuarioId"
            JOIN "Institucion" i ON i."Id" = u."InstitucionId"
            WHERE v."TokenHash" = {hash} AND v."Utilizada" IS NULL
              AND v."Vencimiento" > clock_timestamp()
              AND NOT u."Activo" AND u."PasswordHash" = '!pendiente-activacion'
              AND u."Rol" = 'Alumno' AND i."Activo"
            FOR UPDATE OF v, u FOR SHARE OF i
            """).ToListAsync(ct);
        if (ids.Count != 1) throw new UnauthorizedAccessException();

        var id = ids[0];
        var consumida = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InvitacionUsuario" SET "Utilizada" = clock_timestamp()
            WHERE "TokenHash" = {hash} AND "Utilizada" IS NULL
              AND "Vencimiento" > clock_timestamp()
            """, ct);
        if (consumida != 1) throw new UnauthorizedAccessException();

        var activada = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Usuario" SET "PasswordHash" = {passwordHash}, "Activo" = TRUE
            WHERE "Id" = {id} AND "Rol" = 'Alumno' AND NOT "Activo"
              AND "PasswordHash" = '!pendiente-activacion'
            """, ct);
        if (activada != 1) throw new UnauthorizedAccessException();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "EntregaInvitacion" SET "ContenidoProtegido" = ''
            WHERE "InvitacionId" = (SELECT "Id" FROM "InvitacionUsuario" WHERE "TokenHash" = {hash})
            """, ct);
        await tx.CommitAsync(ct);
    }
}
