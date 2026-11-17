using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.Infrastructure.Plataforma;

public sealed class ImportacionAlumnosDataAccess(
    PlataformaDbContext db,
    IDataProtectionProvider protection,
    InstitutionalOptions options,
    IHostEnvironment environment) : IImportacionAlumnosDataAccess
{
    public const string ProtectionPurpose = PlataformaDataAccess.ProtectionPurpose;
    private readonly Uri activationUri = options.GetActivationUri(environment.IsDevelopment());

    public async Task<ResultadoPersistenciaAlumno> CrearPendienteAsync(
        Guid administradorId,
        Guid institucionId,
        string nombre,
        string apellido,
        string email,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var actores = await db.Database.SqlQuery<Guid>($"""
                SELECT u."Id" AS "Value" FROM "Usuario" u
                JOIN "Institucion" i ON i."Id" = u."InstitucionId"
                WHERE u."Id" = {administradorId} AND i."Id" = {institucionId}
                  AND u."Rol" = {IdentityRoles.Administrador} AND u."Activo" AND i."Activo"
                FOR SHARE OF u, i
                """).ToListAsync(ct);
            if (administradorId == Guid.Empty || institucionId == Guid.Empty || actores.Count != 1)
                throw new UnauthorizedAccessException("El administrador o la institución ya no están activos.");

            var normalizado = await db.Database.SqlQuery<string>($"SELECT pei_normalize_email({email}) AS \"Value\"").SingleAsync(ct);
            var existente = await db.Database.SqlQuery<bool>($"""
                SELECT EXISTS (SELECT 1 FROM "Usuario" WHERE "EmailNormalizado" = {normalizado}) AS "Value"
                """).SingleAsync(ct);
            if (existente)
            {
                await tx.CommitAsync(ct);
                return new(normalizado, ResultadoAltaAlumno.Omitido);
            }

            var usuarioId = Guid.NewGuid();
            var invitacionId = Guid.NewGuid();
            var ahora = DateTime.UtcNow;
            var token = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            var hash = PlataformaDataAccess.HashToken(token);
            var link = new UriBuilder(activationUri) { Fragment = $"token={token}" }.Uri.ToString();
            var protegido = protection.CreateProtector(ProtectionPurpose)
                .Protect($"Activá tu cuenta de Tiza dentro de 24 horas: {link}");

            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Usuario" ("Id","InstitucionId","Email","PasswordHash","Nombre","Apellido","Rol","FechaCreacion","Activo")
                VALUES ({usuarioId},{institucionId},{email},'!pendiente-activacion',{nombre},{apellido},{IdentityRoles.Alumno},{ahora},FALSE)
                """, ct);
            db.Add(new InvitacionAdministrador
            {
                Id = invitacionId,
                UsuarioId = usuarioId,
                TokenHash = hash,
                Vencimiento = ahora.AddHours(24)
            });
            db.Add(new EntregaInvitacion
            {
                Id = Guid.NewGuid(),
                InvitacionId = invitacionId,
                Destinatario = email,
                ContenidoProtegido = protegido,
                Creada = ahora
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(normalizado, ResultadoAltaAlumno.Creado);
        }
        catch (Exception ex) when (EsViolacionUnicaEmail(ex))
        {
            db.ChangeTracker.Clear();
            await tx.RollbackAsync(ct);
            return new(await NormalizarEmailAsync(email, ct), ResultadoAltaAlumno.Omitido);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public Task<string> NormalizarEmailAsync(string email, CancellationToken ct) =>
        db.Database.SqlQuery<string>($"SELECT pei_normalize_email({email}) AS \"Value\"").SingleAsync(ct);

    private static bool EsViolacionUnicaEmail(Exception exception)
    {
        for (Exception? actual = exception; actual is not null; actual = actual.InnerException)
            if (actual is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Usuario_EmailNormalizado" })
                return true;
        return false;
    }
}
