using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Authentication;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.IntegrationTests;

// Usa exactamente el fixture, verificacion del destino y aislamiento por esquema existentes.
public sealed partial class TenantIsolationTests
{
    private static Usuario NuevoUsuario(Guid institucion, string email) => new()
    {
        InstitucionId = institucion, Email = email, Nombre = "Prueba", Apellido = "Email",
        PasswordHash = "ficticio", Rol = "Docente"
    };

    private static Task<int> InsertarEmailSql(IdentidadDbContext db, Guid institucion, string email) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Usuario" ("Id","InstitucionId","Email","PasswordHash","Nombre","Apellido","Rol","FechaCreacion","Activo")
            VALUES ({Guid.NewGuid()},{institucion},{email},'ficticio','Prueba','Email','Docente',{DateTime.UtcNow},TRUE)
            """);

    [DatabaseFact]
    public async Task LoginNormalizaMayusculasYEspaciosDeCualquierProveedor()
    {
        foreach (var email in new[] { "Profesor@gmail.com", "Persona@outlook.com", "Docente@escuela.edu.ar", "Nombre+curso@dominio-propio.org" })
        {
            await using (var db = Context(a))
            {
                db.Usuarios.Add(NuevoUsuario(a, "  " + email + "\t"));
                await db.SaveChangesAsync();
            }
            await using var loginDb = Context(null);
            var login = await new AuthenticationDataAccess(loginDb).BuscarUsuarioParaLoginAsync("\u00a0\t" + email.ToUpperInvariant() + "\u3000");
            Assert.NotNull(login);
            Assert.Equal(email, login.Email);
            Assert.Equal(a, login.InstitucionId);
        }
    }

    [DatabaseFact]
    public async Task DuplicadosEntreInstitucionesRechazadosPorPostgres()
    {
        await using (var db = Context(a))
        {
            db.Usuarios.Add(NuevoUsuario(a, "profesor@gmail.com"));
            await db.SaveChangesAsync();
        }
        foreach (var institucion in new[] { a, b })
        foreach (var email in new[] { "PROFESOR@gmail.com", "  profesor@gmail.com\t", "\u00a0PROFESOR@GMAIL.COM\u3000" })
        {
            await using var db = Context(institucion);
            db.Usuarios.Add(NuevoUsuario(institucion, email));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var postgres = Assert.IsType<PostgresException>(error.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
            Assert.Equal("IX_Usuario_EmailNormalizado", postgres.ConstraintName);
        }
        await using var verificar = Context(a);
        Assert.Single(await verificar.Usuarios.IgnoreQueryFilters().Where(u => u.Email == "profesor@gmail.com").ToListAsync());
    }

    [DatabaseFact]
    public async Task SqlDirectoYActualizacionMasivaRespetanNormalizacion()
    {
        await using var db = Context(a);
        await InsertarEmailSql(db, a, "\t\u00a0Profesor@dominio.org\u3000");
        await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, "  Otro@dominio.org\t"));
        await using (var verificar = Context(a))
        {
            Assert.Equal("Otro@dominio.org", (await verificar.Usuarios.SingleAsync(u => u.Id == ua)).Email);
            Assert.Equal("Profesor@dominio.org", await verificar.Database.SqlQueryRaw<string>("""
                SELECT "Email" AS "Value" FROM "Usuario" WHERE "EmailNormalizado" = 'profesor@dominio.org'
                """).SingleAsync());
            Assert.NotNull(await new AuthenticationDataAccess(verificar).BuscarUsuarioParaLoginAsync("PROFESOR@DOMINIO.ORG"));
        }
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertarEmailSql(db, b, "PROFESOR@DOMINIO.ORG"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        error = await Assert.ThrowsAsync<PostgresException>(() => db.Usuarios.Where(u => u.Id == ua)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, "  PROFESOR@DOMINIO.ORG  ")));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        await using var final = Context(a);
        Assert.Equal("Otro@dominio.org", (await final.Usuarios.SingleAsync(u => u.Id == ua)).Email);
    }

    [DatabaseFact]
    public async Task RegistroConcurrenteDeEmailTieneUnSoloGanador()
    {
        async Task<bool> Registrar(Guid institucion, string email)
        {
            await using var db = Context(institucion);
            db.Usuarios.Add(NuevoUsuario(institucion, email));
            try { await db.SaveChangesAsync(); return true; }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return false; }
        }
        var resultados = await Task.WhenAll(Registrar(a, "Persona@cualquier.org"), Registrar(b, " PERSONA@CUALQUIER.ORG "));
        Assert.Single(resultados.Where(r => r));
    }

    [DatabaseFact]
    public async Task LoginNormalizadoSigueRechazandoUsuarioOInstitucionInactivos()
    {
        await using var db = Context(a);
        var usuario = await db.Usuarios.SingleAsync(u => u.Id == ua);
        usuario.Activo = false;
        await db.SaveChangesAsync();
        var login = new AuthenticationDataAccess(db);
        Assert.Null(await login.BuscarUsuarioParaLoginAsync("  " + usuario.Email.ToUpperInvariant() + "  "));
        usuario.Activo = true;
        await db.SaveChangesAsync();
        await db.Instituciones.ExecuteUpdateAsync(s => s.SetProperty(i => i.Activo, false));
        Assert.Null(await login.BuscarUsuarioParaLoginAsync(usuario.Email.ToUpperInvariant()));
    }

    [DatabaseFact]
    public async Task MigracionConColisionesAbortaSinModificarDatos()
    {
        await using var db = Context(null);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261020000000_ProtectIdentityInvariants");
        await InsertarEmailSql(db, a, " Profesor@legacy.org ");
        await InsertarEmailSql(db, b, "PROFESOR@LEGACY.ORG");
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        // No usar el modelo nuevo contra el esquema anterior: verificamos valores crudos.
        var emails = await db.Database.SqlQueryRaw<string>("""
            SELECT "Email" AS "Value" FROM "Usuario" WHERE "Email" ILIKE '%legacy.org%'
            """).ToListAsync();
        Assert.Contains(" Profesor@legacy.org ", emails);
        Assert.Contains("PROFESOR@LEGACY.ORG", emails);
        Assert.DoesNotContain("20261020143631_NormalizeGlobalEmails", await db.Database.GetAppliedMigrationsAsync());
    }

    [DatabaseFact]
    public async Task MigracionSinColisionesRecortaDatosExistentes()
    {
        await using var db = Context(null);
        await db.GetService<IMigrator>().MigrateAsync("20261020000000_ProtectIdentityInvariants");
        await InsertarEmailSql(db, a, "\u00a0Legacy@proveedor.org\t");
        await db.Database.MigrateAsync();
        await using var verificar = Context(a);
        Assert.NotNull(await new AuthenticationDataAccess(verificar).BuscarUsuarioParaLoginAsync("LEGACY@PROVEEDOR.ORG"));
        var almacenado = await verificar.Database.SqlQueryRaw<string>("""
            SELECT "Email" AS "Value" FROM "Usuario" WHERE "Email" = 'Legacy@proveedor.org'
            """).SingleAsync();
        Assert.Equal("Legacy@proveedor.org", almacenado);
    }
}
