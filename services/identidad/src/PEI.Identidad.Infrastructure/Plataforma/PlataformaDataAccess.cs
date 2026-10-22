using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.Infrastructure.Plataforma;

// Puerto privilegiado acotado: no expone un DbContext sin filtros a los consumidores.
public sealed class PlataformaDataAccess(PlataformaDbContext db, IDataProtectionProvider protection, PlataformaOptions options) : IPlataformaDataAccess
{
    public const string ProtectionPurpose = "Tiza.ID06.Invitaciones.v1";
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public Task<CuentaPlataforma?> BuscarLoginAsync(string email, CancellationToken ct) => db.CuentasPlataforma.AsNoTracking()
        .SingleOrDefaultAsync(c => EF.Property<string>(c, "EmailNormalizado") == EmailDbFunctions.Normalize(email) && c.Activo && c.Rol == "AdministradorGlobal", ct);

    public Task<bool> EstaActivaAsync(Guid id, CancellationToken ct) => db.CuentasPlataforma.AsNoTracking()
        .AnyAsync(c => c.Id == id && c.Activo && c.Rol == "AdministradorGlobal", ct);

    private async Task ValidarActorAsync(Guid actor, CancellationToken ct)
    {
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT "Id" AS "Value" FROM "CuentaPlataforma"
            WHERE "Id" = {actor} AND "Activo" AND "Rol" = 'AdministradorGlobal' FOR SHARE
            """).ToListAsync(ct);
        if (ids.Count != 1) throw new UnauthorizedAccessException();
    }

    public async Task<InstitucionCreada> CrearInstitucionAsync(Guid actor, CrearInstitucionCommand command, CancellationToken ct)
    {
        var institucion = Guid.NewGuid(); var usuario = Guid.NewGuid(); var invitacion = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var token = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var hash = HashToken(token);
        var contenido = protection.CreateProtector(ProtectionPurpose).Protect($"Activá tu cuenta de Tiza dentro de 24 horas: {options.ActivationUrl}#token={token}");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await ValidarActorAsync(actor, ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Institucion" ("Id","Nombre","Codigo","FechaCreacion","Activo")
                VALUES ({institucion},{command.Nombre.Trim()},{command.Codigo.Trim()},{now},TRUE)
                """, ct);
            // Usuario inactivo y sin credencial utilizable hasta consumir la invitación.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Usuario" ("Id","InstitucionId","Email","PasswordHash","Nombre","Apellido","Rol","FechaCreacion","Activo")
                VALUES ({usuario},{institucion},{command.EmailAdministrador},'!pendiente-activacion',
                    {command.NombreAdministrador.Trim()},{command.ApellidoAdministrador.Trim()},'Administrador',{now},FALSE)
                """, ct);
            db.Add(new InvitacionAdministrador { Id = invitacion, UsuarioId = usuario, TokenHash = hash, Vencimiento = now.AddHours(24) });
            db.Add(new EntregaInvitacion { Id = Guid.NewGuid(), InvitacionId = invitacion, Destinatario = command.EmailAdministrador.Trim(),
                ContenidoProtegido = contenido, Creada = now });
            await db.SaveChangesAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AuditoriaPlataforma" ("Id","ActorId","InstitucionId","UsuarioId","Evento","Fecha")
                VALUES ({Guid.NewGuid()},{actor},{institucion},{usuario},'InstitucionCreada',{now})
                """, ct);
            await tx.CommitAsync(ct);
            return new(institucion, usuario);
        }
        catch (Exception ex) when (ex is PostgresException { SqlState: "23505" } || ex is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } })
        { throw new RegistrationConflictException(); }
    }

    public async Task<IReadOnlyList<InstitucionResumen>> ListarInstitucionesAsync(Guid actor, int pagina, CancellationToken ct)
    {
        if (pagina is < 1 or > 1000000) throw new ArgumentException("Página inválida.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ValidarActorAsync(actor, ct);
        var filas = await db.Database.SqlQuery<InstitucionResumen>($"""
            SELECT "Id", "Nombre", "Codigo", "Activo" FROM "Institucion" ORDER BY "Id" LIMIT 50 OFFSET {(pagina - 1) * 50}
            """).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return filas;
    }

    public async Task ActivarAsync(string token, string passwordHash, CancellationToken ct)
    {
        if (token.Length != 43 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) throw new UnauthorizedAccessException();
        var hash = HashToken(token);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Bloqueo estable de institución, usuario e invitación; una sola activación gana.
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT v."UsuarioId" AS "Value" FROM "InvitacionAdministrador" v
            JOIN "Usuario" u ON u."Id" = v."UsuarioId" JOIN "Institucion" i ON i."Id" = u."InstitucionId"
            WHERE v."TokenHash" = {hash} AND v."Utilizada" IS NULL AND v."Vencimiento" > clock_timestamp()
              AND NOT u."Activo" AND u."PasswordHash" = '!pendiente-activacion' AND u."Rol" = 'Administrador' AND i."Activo"
            FOR UPDATE OF v, u FOR SHARE OF i
            """).ToListAsync(ct);
        if (ids.Count != 1) throw new UnauthorizedAccessException();
        var id = ids[0];
        // Revalidar el vencimiento después de cualquier espera por locks.
        var consumida = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "InvitacionAdministrador" SET "Utilizada" = clock_timestamp()
            WHERE "TokenHash" = {hash} AND "Utilizada" IS NULL AND "Vencimiento" > clock_timestamp()
            """, ct);
        if (consumida != 1) throw new UnauthorizedAccessException();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Usuario" SET "PasswordHash" = {passwordHash}, "Activo" = TRUE WHERE "Id" = {id}
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "EntregaInvitacion" SET "ContenidoProtegido" = ''
            WHERE "InvitacionId" IN (SELECT "Id" FROM "InvitacionAdministrador" WHERE "TokenHash" = {hash})
            """, ct);
        await tx.CommitAsync(ct);
    }
}
