using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.DevelopmentSeed;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    // Random credentials generated inside the test process, never production secrets or catalog defaults.
    private readonly string seedPassword = Guid.NewGuid().ToString("N");
    private SeedOptions SeedConfiguration(bool credentials = true, bool enabled = true, string? targetSchema = null) => new()
    {
        EnvironmentName = "Testing", Enabled = enabled, Operator = "ID08-integration-tests",
        ConnectionString = connection, Schema = targetSchema ?? schema,
        AllowedDestinations = [new(new NpgsqlConnectionStringBuilder(connection).Host!, 5541, "pei_identidad_tests", schema)],
        Passwords = credentials ? SeedCatalog.Users.ToDictionary(u => u.Id, _ => seedPassword) : new Dictionary<Guid, string>()
    };
    private async Task PrepareSeedSchema()
    {
        await using var platform = PlatformContext();
        await platform.Database.MigrateAsync();
    }

    [DatabaseFact]
    public async Task ID08_PublicNoPermiteRedirigirCargaDeDevelopmentALaBaseDeTests()
    {
        await PrepareSeedSchema();
        var before = await SeedSnapshot();
        // Se conserva el servidor/base de tests. La excepción public no admite otra base,
        // aunque el operador la incorpore a la lista y habilite ambas autorizaciones.
        var options = new SeedOptions
        {
            EnvironmentName = "Development", Enabled = true, AllowDevelopmentPublic = true,
            Operator = "ID08-test", ConnectionString = connection, Schema = "public",
            AllowedDestinations = [new("localhost", 5541, "pei_identidad_tests", "public")]
        };
        await Assert.ThrowsAsync<SeedRejectedException>(() => new IdentitySeedRunner(new BCryptPasswordHasher()).RunAsync(options));
        Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_ApiConsultaLasOchoIdentidadesSembradasSinCambiarAutenticacion()
    {
        await PrepareSeedSchema(); await Seed();
        using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        foreach (var user in SeedCatalog.Users)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = user.Email, Password = seedPassword });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var session = await response.Content.ReadFromJsonAsync<JsonElement>();
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.GetProperty("accessToken").GetString());
            Assert.Contains(jwt.Claims, c => c.Type == "sub" && c.Value == user.Id.ToString());
            Assert.Contains(jwt.Claims, c => c.Type == "institucion_id" && c.Value == user.InstitucionId.ToString());
            Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == user.Rol);
        }
    }
    private Task<SeedReport> Seed(bool credentials = true) => new IdentitySeedRunner(new BCryptPasswordHasher()).RunAsync(SeedConfiguration(credentials));

    private async Task<string[]> SeedSnapshot()
    {
        await using var db = Context(null);
        // Full row JSON plus MVCC version: includes hashes, dates, roles, relations and status.
        return (await db.Database.SqlQueryRaw<string>("""
            SELECT row_to_json(i)::text || ':' || i.xmin::text AS "Value" FROM "Institucion" i
            UNION ALL SELECT row_to_json(u)::text || ':' || u.xmin::text FROM "Usuario" u
            UNION ALL SELECT row_to_json(r)::text || ':' || r.xmin::text FROM "RegistroEmail" r
            """).ToListAsync()).Order().ToArray();
    }

    [DatabaseFact]
    public async Task ID08_CatalogoExactoCredencialesAislamientoYNoEfectosLaterales()
    {
        await PrepareSeedSchema();
        var before = await SeedSnapshot();
        var report = await Seed();
        Assert.Equal(new SeedReport(SeedCatalog.Version, 10, 0, 0), report);
        await using var all = Context(null);
        var institutionIds = SeedCatalog.Institutions.Select(i => i.Id).ToArray();
        var userIds = SeedCatalog.Users.Select(u => u.Id).ToArray();
        var institutions = await all.Instituciones.IgnoreQueryFilters().Where(i => institutionIds.Contains(i.Id)).ToListAsync();
        var users = await all.Usuarios.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id)).ToListAsync();
        Assert.Equal(2, institutions.Count); Assert.Equal(8, users.Count);
        foreach (var item in SeedCatalog.Users)
        {
            var row = Assert.Single(users, u => u.Id == item.Id);
            Assert.Equal(item.Email, row.Email); Assert.Equal(item.Rol, row.Rol);
            Assert.Equal(item.InstitucionId, row.InstitucionId); Assert.True(row.Activo);
            Assert.True(new BCryptPasswordHasher().Verify(seedPassword, row.PasswordHash));
            Assert.NotEqual(seedPassword, row.PasswordHash);
        }
        Assert.All(institutions, i => Assert.True(i.Activo));
        await using var tenantA = Context(institutionIds[0]);
        await using var tenantB = Context(institutionIds[1]);
        Assert.Equal(4, await tenantA.Usuarios.CountAsync()); Assert.Equal(4, await tenantB.Usuarios.CountAsync());
        Assert.Empty(await all.Usuarios.ToListAsync());
        tenantA.Usuarios.Add(new Usuario { InstitucionId = institutionIds[1] });
        await Assert.ThrowsAsync<TenantAccessDeniedException>(() => tenantA.SaveChangesAsync());
        var after = await SeedSnapshot();
        Assert.All(before, value => Assert.Contains(value, after));
        Assert.Equal(0, await all.Database.SqlQueryRaw<int>("""
            SELECT ((SELECT count(*) FROM "CuentaPlataforma") + (SELECT count(*) FROM "InvitacionAdministrador") +
                (SELECT count(*) FROM "EntregaInvitacion") + (SELECT count(*) FROM "BootstrapPlataforma") +
                (SELECT count(*) FROM "AuditoriaPlataforma"))::int AS "Value"
            """).SingleAsync());
        // Existing fixture token survives; the seed adds none.
        Assert.Equal(1, await all.RefreshTokens.IgnoreQueryFilters().CountAsync());
    }

    [DatabaseFact]
    public async Task ID08_IdempotenciaPreservaFilasHashesFechasXminYSinCredenciales()
    {
        await PrepareSeedSchema(); await Seed();
        var before = await SeedSnapshot();
        Assert.Equal(new SeedReport(SeedCatalog.Version, 0, 10, 0), await Seed(false));
        Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_CargaParcialCompatible()
    {
        await PrepareSeedSchema();
        await using var db = Context(null);
        var institution = SeedCatalog.Institutions[0]; var user = SeedCatalog.Users[0];
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Institucion" ("Id","Nombre","Codigo","FechaCreacion","Activo")
            VALUES ({institution.Id},{institution.Nombre},{institution.Codigo},{DateTime.UtcNow},TRUE)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Usuario" ("Id","InstitucionId","Email","PasswordHash","Nombre","Apellido","Rol","FechaCreacion","Activo")
            VALUES ({user.Id},{user.InstitucionId},{user.Email},{new BCryptPasswordHasher().Hash(seedPassword)},
                {user.Nombre},{user.Apellido},{user.Rol},{DateTime.UtcNow},TRUE)
            """);
        var before = await SeedSnapshot();
        Assert.Equal(new SeedReport(SeedCatalog.Version, 8, 2, 0), await Seed());
        var after = await SeedSnapshot();
        Assert.All(before, row => Assert.Contains(row, after));
    }

    [DatabaseFact]
    public async Task ID08_CredencialesAusentesNoEscriben()
    {
        await PrepareSeedSchema(); var before = await SeedSnapshot();
        await Assert.ThrowsAsync<SeedRejectedException>(() => Seed(false));
        Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_ColisionesDeCodigoYUuidAbortan()
    {
        await PrepareSeedSchema();
        await using var db = Context(a);
        await db.Instituciones.ExecuteUpdateAsync(s => s.SetProperty(i => i.Codigo, SeedCatalog.Institutions[0].Codigo));
        var before = await SeedSnapshot();
        await Assert.ThrowsAsync<SeedRejectedException>(() => Seed()); Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_CorreoNormalizadoAjenoAbortado()
    {
        await PrepareSeedSchema();
        await using var db = Context(a);
        await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, "  ADMIN@A.TEST  "));
        var before = await SeedSnapshot();
        await Assert.ThrowsAsync<SeedRejectedException>(() => Seed()); Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_ReservaHistoricaImpideResucitarCuenta()
    {
        await PrepareSeedSchema();
        await using var db = Context(a);
        await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, "admin@a.test"));
        await db.Usuarios.Where(u => u.Id == ua).ExecuteDeleteAsync();
        var before = await SeedSnapshot();
        await Assert.ThrowsAsync<SeedRejectedException>(() => Seed()); Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_RollbackSiFallaUltimaInsercion()
    {
        await PrepareSeedSchema();
        await using var db = Context(null);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION id08_test_failure() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW."Email" = 'alumno2@b.test' THEN RAISE EXCEPTION 'failure'; END IF; RETURN NEW; END; $$;
            CREATE TRIGGER id08_test_failure BEFORE INSERT ON "Usuario" FOR EACH ROW EXECUTE FUNCTION id08_test_failure();
            """);
        var before = await SeedSnapshot();
        await Assert.ThrowsAsync<PostgresException>(() => Seed());
        Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_CargadoresConcurrentes()
    {
        await PrepareSeedSchema();
        await using var blocker = new NpgsqlConnection(connection); await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(20061008)", blocker, transaction))
            await command.ExecuteNonQueryAsync();
        var first = Seed(); var second = Seed();
        await using var observer = new NpgsqlConnection(connection); await observer.OpenAsync();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            await using var command = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity WHERE datname=current_database()
                  AND query='SELECT pg_advisory_xact_lock(20061008)' AND wait_event_type='Lock'
                  AND cardinality(pg_blocking_pids(pid)) > 0
                """, observer);
            if ((long)(await command.ExecuteScalarAsync())! >= 2) break;
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10), "Ambos cargadores deben alcanzar el lock.");
            await Task.Delay(25);
        }
        Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
        await transaction.CommitAsync();
        var reports = await Task.WhenAll(first, second);
        Assert.Equal(new[] { 0, 10 }, reports.Select(r => r.Created).Order());
        Assert.Equal(10, reports.Sum(r => r.Created));
    }

    [DatabaseFact]
    public async Task ID08_EntornoDestinoYPrerrequisitosNoAutorizados()
    {
        var before = await ContextSnapshotWithoutPlatform();
        await Assert.ThrowsAnyAsync<Exception>(() => Seed()); // Platform migrations absent.
        Assert.Equal(before, await ContextSnapshotWithoutPlatform());
        await PrepareSeedSchema();
        await Assert.ThrowsAsync<SeedRejectedException>(() => new IdentitySeedRunner(new BCryptPasswordHasher()).RunAsync(SeedConfiguration(enabled: false)));
        await Assert.ThrowsAsync<SeedRejectedException>(() => new IdentitySeedRunner(new BCryptPasswordHasher()).RunAsync(SeedConfiguration(targetSchema: "public")));
    }
    private async Task<int> ContextSnapshotWithoutPlatform()
    {
        await using var db = Context(null);
        return await db.Usuarios.IgnoreQueryFilters().CountAsync();
    }

    [DatabaseFact]
    public async Task ID08_TriggerDeshabilitadoRechazado()
    {
        await PrepareSeedSchema();
        await using var db = Context(null);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Usuario\" DISABLE TRIGGER pei_usuario_reservar_email");
        var before = await SeedSnapshot();
        await Assert.ThrowsAsync<SeedRejectedException>(() => Seed()); Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_UuidConDatosIncompatiblesNoSeCorrige()
    {
        await PrepareSeedSchema(); await Seed();
        await using var db = Context(SeedCatalog.Institutions[0].Id);
        await db.Usuarios.Where(u => u.Id == SeedCatalog.Users[0].Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Rol, "Docente").SetProperty(u => u.Activo, false));
        var before = await SeedSnapshot();
        await Assert.ThrowsAsync<SeedRejectedException>(() => Seed()); Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_CorreoDeCuentaGlobalNoSeReutiliza()
    {
        await PrepareSeedSchema();
        await using var db = PlatformContext();
        db.Add(new CuentaPlataforma { Email = "ADMIN@B.TEST", Nombre = "Global", Apellido = "Prueba",
            PasswordHash = new BCryptPasswordHasher().Hash(seedPassword) });
        await db.SaveChangesAsync();
        var before = await SeedSnapshot();
        await Assert.ThrowsAsync<SeedRejectedException>(() => Seed()); Assert.Equal(before, await SeedSnapshot());
        Assert.Single(await db.CuentasPlataforma.ToListAsync());
    }

    [DatabaseFact]
    public async Task ID08_IndiceUnicoAusenteNoSeTolera()
    {
        await PrepareSeedSchema();
        await using var db = Context(null);
        await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_Usuario_EmailNormalizado\"");
        var before = await SeedSnapshot();
        await Assert.ThrowsAsync<SeedRejectedException>(() => Seed()); Assert.Equal(before, await SeedSnapshot());
    }

    [DatabaseFact]
    public async Task ID08_ClaveInvalidaNoFiltraValorNiEscribe()
    {
        await PrepareSeedSchema(); var before = await SeedSnapshot();
        var options = SeedConfiguration();
        var invalid = new string('z', 73);
        ((Dictionary<Guid, string>)options.Passwords)[SeedCatalog.Users[^1].Id] = invalid;
        var error = await Assert.ThrowsAsync<SeedRejectedException>(() => new IdentitySeedRunner(new BCryptPasswordHasher()).RunAsync(options));
        Assert.DoesNotContain(invalid, error.Message); Assert.Equal(before, await SeedSnapshot());
    }
}
