using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Plataforma;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    private readonly IDataProtectionProvider platformProtection = new EphemeralDataProtectionProvider();
    private static PlataformaOptions PlatformOptions() => new() { ActivationUrl = "https://tiza.test/activar" };
    private PlataformaDbContext PlatformContext() => new(new DbContextOptionsBuilder<PlataformaDbContext>()
        .UseNpgsql(connection, n => n.MigrationsHistoryTable("__EFMigrationsHistoryPlataforma", schema)).Options);
    private PlataformaDataAccess PlatformData(PlataformaDbContext db) => new(db, platformProtection, PlatformOptions());
    private async Task<Guid> InicializarPlataformaAsync()
    {
        await using var db = PlatformContext();
        await db.Database.MigrateAsync();
        var cuenta = new CuentaPlataforma { Email = "global@tiza.test", PasswordHash = new BCryptPasswordHasher().Hash("GlobalSegura123!"), Nombre = "Global", Apellido = "Prueba" };
        db.Add(cuenta); await db.SaveChangesAsync(); return cuenta.Id;
    }
    private static CrearInstitucionCommand Alta(string email = "primer.admin@tiza.test") => new()
    { Nombre = "Institución", Codigo = Guid.NewGuid().ToString("N")[..20], EmailAdministrador = email, NombreAdministrador = "Admin", ApellidoAdministrador = "Inicial" };
    private async Task<string> TokenInvitacionAsync(PlataformaDbContext db)
    {
        var entrega = await db.Set<EntregaInvitacion>().SingleAsync();
        return platformProtection.CreateProtector(PlataformaDataAccess.ProtectionPurpose).Unprotect(entrega.ContenidoProtegido).Split("#token=")[1];
    }
    private static async Task AssertUnique(Func<Task> action)
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(action);
        Assert.Equal("23505", (ex as PostgresException ?? ex.InnerException as PostgresException)?.SqlState);
    }

    [DatabaseFact]
    public async Task PlataformaBackfillYReservasHistoricasNoSeLiberan()
    {
        await InicializarPlataformaAsync();
        await using var db = PlatformContext();
        Assert.Equal(4, await db.RegistrosEmail.CountAsync());
        await using var institucional = Context(a);
        var original = ua + "@test.invalid";
        await institucional.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Usuario" SET "Email" = 'nuevo@tiza.test', "Activo" = FALSE WHERE "Id" = {ua}""");
        await institucional.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Usuario" SET "Email" = {original} WHERE "Id" = {ua}""");
        await institucional.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Usuario" WHERE "Id" = {ua}""");
        Assert.Equal(5, await db.RegistrosEmail.CountAsync());
        await AssertUnique(async () => { db.Add(new CuentaPlataforma { Email = " NUEVO@tiza.test " }); await db.SaveChangesAsync(); });
    }

    [DatabaseFact]
    public async Task PlataformaReservaEntreClasesYRetornoACorreoPropio()
    {
        var actor = await InicializarPlataformaAsync();
        await using var db = PlatformContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "CuentaPlataforma" SET "Email" = 'otro@tiza.test' WHERE "Id" = {actor}""");
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "CuentaPlataforma" SET "Email" = ' GLOBAL@TIZA.TEST ' WHERE "Id" = {actor}""");
        Assert.Equal("GLOBAL@TIZA.TEST", (await db.CuentasPlataforma.SingleAsync()).Email);
        await Assert.ThrowsAsync<RegistrationConflictException>(() => PlatformData(db).CrearInstitucionAsync(actor, Alta("otro@tiza.test"), default));
        await using var verificar = Context(null);
        Assert.Equal(2, await verificar.Instituciones.IgnoreQueryFilters().CountAsync());
        Assert.Empty(await db.Set<InvitacionAdministrador>().AsNoTracking().ToListAsync());
    }

    [DatabaseFact]
    public async Task PlataformaReservasRechazanManipulacionDirectaYTruncate()
    {
        await InicializarPlataformaAsync();
        await using var db = PlatformContext();
        foreach (var sql in new[] { "DELETE FROM \"RegistroEmail\"", "UPDATE \"RegistroEmail\" SET \"TipoCuenta\" = 'Plataforma'", "TRUNCATE \"RegistroEmail\"",
            "INSERT INTO \"RegistroEmail\" VALUES ('libre@tiza.test','Plataforma','00000000-0000-0000-0000-000000000001')" })
        {
            var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
            Assert.Equal("23514", ex.SqlState);
        }
    }

    [DatabaseFact]
    public async Task PlataformaReservaConcurrenteEntreClasesUnGanador()
    {
        await InicializarPlataformaAsync();
        async Task<bool> Plataforma()
        {
            await using var db = PlatformContext();
            db.Add(new CuentaPlataforma { Email = "carrera@tiza.test" });
            try { await db.SaveChangesAsync(); return true; } catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { return false; }
        }
        async Task<bool> Institucional()
        {
            await using var db = Context(a); db.Add(new Usuario { InstitucionId = a, Email = " CARRERA@tiza.test ", Rol = "Docente" });
            try { await db.SaveChangesAsync(); return true; } catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { return false; }
        }
        Assert.Single((await Task.WhenAll(Plataforma(), Institucional())).Where(x => x));
    }

    [DatabaseFact]
    public async Task PlataformaCambiosConcurrentesDeCorreoUnGanador()
    {
        var actor = await InicializarPlataformaAsync();
        async Task<bool> Cambiar(bool global)
        {
            await using var db = PlatformContext();
            try
            {
                if (global) await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "CuentaPlataforma" SET "Email" = 'comun@tiza.test' WHERE "Id" = {actor}""");
                else await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Usuario" SET "Email" = ' COMUN@tiza.test ' WHERE "Id" = {ua}""");
                return true;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") { return false; }
        }
        Assert.Single((await Task.WhenAll(Cambiar(true), Cambiar(false))).Where(x => x));
        await using var verificar = PlatformContext();
        Assert.Equal(5, await verificar.RegistrosEmail.CountAsync());
    }

    [DatabaseFact]
    public async Task PlataformaAltaAtomicaInvitacionHashYActivacionUnica()
    {
        var actor = await InicializarPlataformaAsync();
        await using var db = PlatformContext(); var data = PlatformData(db);
        var creada = await data.CrearInstitucionAsync(actor, Alta(), default);
        var token = await TokenInvitacionAsync(db);
        var invitacion = await db.Set<InvitacionAdministrador>().SingleAsync();
        Assert.Equal(64, invitacion.TokenHash.Length); Assert.DoesNotContain(token, invitacion.TokenHash);
        Assert.InRange(invitacion.Vencimiento, DateTime.UtcNow.AddHours(23.99), DateTime.UtcNow.AddHours(24));
        await using var institucional = Context(creada.InstitucionId);
        Assert.False((await institucional.Usuarios.SingleAsync()).Activo);
        var hash = new BCryptPasswordHasher().Hash("Activada123!");
        await data.ActivarAsync(token, hash, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => data.ActivarAsync(token, hash, default));
        institucional.ChangeTracker.Clear();
        var usuario = await institucional.Usuarios.SingleAsync();
        Assert.True(usuario.Activo); Assert.Equal("Administrador", usuario.Rol); Assert.Equal(hash, usuario.PasswordHash);
        await using var ajeno = Context(b);
        Assert.Empty(await ajeno.Usuarios.Where(u => u.Id == creada.AdministradorId).ToListAsync());
    }

    [DatabaseFact]
    public async Task PlataformaActivacionConcurrenteUnGanador()
    {
        var actor = await InicializarPlataformaAsync();
        await using var db = PlatformContext(); await PlatformData(db).CrearInstitucionAsync(actor, Alta(), default);
        var token = await TokenInvitacionAsync(db);
        async Task<bool> Activar()
        {
            await using var otro = PlatformContext();
            try { await PlatformData(otro).ActivarAsync(token, "hash-prueba", default); return true; }
            catch (UnauthorizedAccessException) { return false; }
        }
        Assert.Single((await Task.WhenAll(Activar(), Activar())).Where(x => x));
    }

    [DatabaseFact]
    public async Task PlataformaInvitacionVencidaNoActivaUsuario()
    {
        await InicializarPlataformaAsync();
        await using var db = PlatformContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "Usuario" SET "Activo" = FALSE, "Rol" = 'Administrador', "PasswordHash" = '!pendiente-activacion' WHERE "Id" = {ua}""");
        var token = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        db.Add(new InvitacionAdministrador { Id = Guid.NewGuid(), UsuarioId = ua, TokenHash = PlataformaDataAccess.HashToken(token), Vencimiento = DateTime.UtcNow.AddSeconds(-1) });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => PlatformData(db).ActivarAsync(token, "hash", default));
    }

    [DatabaseFact]
    public async Task PlataformaActorInactivoNoCreaInstitucion()
    {
        var actor = await InicializarPlataformaAsync(); await using var db = PlatformContext();
        await db.CuentasPlataforma.Where(x => x.Id == actor).ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => PlatformData(db).CrearInstitucionAsync(actor, Alta(), default));
        Assert.Empty(await db.Set<InvitacionAdministrador>().ToListAsync());
    }

    [DatabaseFact]
    public async Task PlataformaBootstrapConcurrenteUnaSolaInicializacion()
    {
        await using (var db = PlatformContext()) await db.Database.MigrateAsync();
        async Task<bool> Bootstrap()
        {
            await using var db = PlatformContext();
            try { await new PlataformaBootstrap(db, new BCryptPasswordHasher()).EjecutarAsync("bootstrap@tiza.test", "Segura123!", "Admin", "Global", "operador-test", default); return true; }
            catch (InvalidOperationException) { return false; }
        }
        Assert.Single((await Task.WhenAll(Bootstrap(), Bootstrap())).Where(x => x));
        await using var verificar = PlatformContext();
        await verificar.Database.ExecuteSqlRawAsync("DELETE FROM \"CuentaPlataforma\"");
        Assert.False(await Bootstrap());
        Assert.Equal(4, await verificar.RegistrosEmail.CountAsync());
        await AssertUnique(async () => { verificar.Add(new CuentaPlataforma { Email = "BOOTSTRAP@tiza.test" }); await verificar.SaveChangesAsync(); });
    }

    private sealed class EmailPrueba : IEmailSender
    {
        public bool Fallar { get; set; }
        public List<MensajeEmail> Enviados { get; } = new();
        public Task EnviarAsync(MensajeEmail mensaje, CancellationToken ct)
        {
            if (Fallar) throw new IOException("Fallo simulado"); Enviados.Add(mensaje); return Task.CompletedTask;
        }
    }

    [DatabaseFact]
    public async Task PlataformaOutboxReintentaSinPerderInstitucionNiExponerToken()
    {
        var actor = await InicializarPlataformaAsync();
        await using var db = PlatformContext();
        var creada = await PlatformData(db).CrearInstitucionAsync(actor, Alta(), default);
        var sender = new EmailPrueba { Fallar = true };
        var outbox = new InvitacionesOutbox(db, sender, platformProtection);
        Assert.True(await outbox.ProcesarUnaAsync(default));
        var entrega = await db.Set<EntregaInvitacion>().SingleAsync();
        Assert.Null(entrega.Entregada); Assert.Equal(1, entrega.Intentos); Assert.NotEmpty(entrega.ContenidoProtegido);
        sender.Fallar = false;
        await db.Database.ExecuteSqlRawAsync("UPDATE \"EntregaInvitacion\" SET \"Creada\" = \"Creada\" - interval '2 minutes'");
        db.ChangeTracker.Clear();
        Assert.True(await outbox.ProcesarUnaAsync(default)); Assert.Single(sender.Enviados);
        Assert.False(await outbox.ProcesarUnaAsync(default));
        Assert.Equal("", (await db.Set<EntregaInvitacion>().SingleAsync()).ContenidoProtegido);
        await using var institucional = Context(creada.InstitucionId); Assert.Single(await institucional.Usuarios.ToListAsync());
    }

    [DatabaseFact]
    public async Task PlataformaDownBloqueaPerdidaDeDatosYModeloCoincide()
    {
        await InicializarPlataformaAsync(); await using var db = PlatformContext();
        Assert.False(db.Database.HasPendingModelChanges());
        await using var institucional = Context(null);
        Assert.False(institucional.Database.HasPendingModelChanges());
        await db.GetService<IMigrator>().MigrateAsync("20261020231100_InitialPlatformSchema");
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("0"));
        Assert.True(await db.CuentasPlataforma.AnyAsync());
    }
}
