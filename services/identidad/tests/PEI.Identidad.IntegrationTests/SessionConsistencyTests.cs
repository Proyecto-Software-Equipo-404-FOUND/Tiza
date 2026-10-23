using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Authentication;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    // La actualizacion queda sin commit: la lectura inicial ve la identidad anterior.
    // Se libera solo cuando pg_stat_activity confirma que la sesion espera su lock.
    private async Task H2_CambioConcurrenteAsync(bool refresh, string cambio)
    {
        await PrepararLoginAsync();
        var nombre = schema + "_h2";
        using var factory = new PostgresHttpFactory(ConexionConNombre(nombre), schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        string? anterior = refresh ? (await LoginHttpAsync(client)).GetProperty("refreshToken").GetString()! : null;
        await using var verificar = Context(null);
        var cantidad = await verificar.RefreshTokens.IgnoreQueryFilters().CountAsync();
        await using var observer = new NpgsqlConnection(connection); await observer.OpenAsync();
        await using var blocker = new NpgsqlConnection(connection); await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        var sql = cambio switch
        {
            "rol" => "UPDATE \"Usuario\" SET \"Rol\" = 'Alumno' WHERE \"Id\" = @id",
            "credencial" => "UPDATE \"Usuario\" SET \"PasswordHash\" = @password WHERE \"Id\" = @id",
            "institucion" => "UPDATE \"Institucion\" SET \"Activo\" = FALSE WHERE \"Id\" = @id",
            _ => "UPDATE \"Usuario\" SET \"Activo\" = FALSE WHERE \"Id\" = @id"
        };
        await using (var update = new NpgsqlCommand(sql, blocker, tx))
        {
            update.Parameters.AddWithValue("id", cambio == "institucion" ? a : ua);
            if (cambio == "credencial") update.Parameters.AddWithValue("password", new BCryptPasswordHasher().Hash("NuevaCredencial123!"));
            Assert.Equal(1, await update.ExecuteNonQueryAsync());
        }
        var responseTask = refresh
            ? client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = anterior })
            : client.PostAsJsonAsync("/api/auth/login", new { Email = ua + "@test.invalid", Password = "Sesion123!" });
        await EsperarBloqueadosAsync(observer, nombre, 1);
        Assert.False(responseTask.IsCompleted);
        await tx.CommitAsync();
        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(15));
        if (cambio == "rol")
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var raw = body.GetProperty("accessToken").GetString()!;
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(raw);
            Assert.Equal("Alumno", Assert.Single(jwt.Claims, c => c.Type == "role").Value);
            Assert.Equal(a.ToString(), Assert.Single(jwt.Claims, c => c.Type == "institucion_id").Value);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", raw);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/alumno")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/docente")).StatusCode);
            Assert.Equal(cantidad + 1, await verificar.RefreshTokens.IgnoreQueryFilters().CountAsync());
        }
        else
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(cantidad, await verificar.RefreshTokens.IgnoreQueryFilters().CountAsync());
            if (refresh)
            {
                var hashToken = new RefreshTokenHasher().Hash(anterior!);
                Assert.False((await verificar.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.Token == hashToken)).Revocado);
            }
        }
    }

    [DatabaseFact] public Task H2_LoginUsaRolConfirmadoDespuesDeLaLecturaInicial() => H2_CambioConcurrenteAsync(false, "rol");
    [DatabaseFact] public Task H2_RefreshUsaRolConfirmadoBajoLocks() => H2_CambioConcurrenteAsync(true, "rol");
    [DatabaseFact] public Task H2_LoginRechazaCredencialModificada() => H2_CambioConcurrenteAsync(false, "credencial");
    [DatabaseFact] public Task H2_LoginRechazaUsuarioDesactivado() => H2_CambioConcurrenteAsync(false, "usuario");
    [DatabaseFact] public Task H2_RefreshRechazaUsuarioDesactivado() => H2_CambioConcurrenteAsync(true, "usuario");
    [DatabaseFact] public Task H2_LoginRechazaInstitucionDesactivada() => H2_CambioConcurrenteAsync(false, "institucion");
    [DatabaseFact] public Task H2_RefreshRechazaInstitucionDesactivada() => H2_CambioConcurrenteAsync(true, "institucion");

    private sealed class EmisorConFallo : IAccessTokenIssuer
    {
        public IssuedAccessToken Issue(Usuario usuario) => throw new InvalidOperationException("fallo-emision-sintetico-confidencial");
    }

    private async Task H2_FalloEmisionAsync(bool refresh)
    {
        await PrepararLoginAsync();
        using var factory = new PostgresHttpFactory(connection, schema);
        using var normal = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var anterior = refresh ? (await LoginHttpAsync(normal)).GetProperty("refreshToken").GetString()! : null;
        await using var verificar = Context(null);
        var cantidad = await verificar.RefreshTokens.IgnoreQueryFilters().CountAsync();
        using var broken = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IAccessTokenIssuer>(); s.AddSingleton<IAccessTokenIssuer, EmisorConFallo>();
        }));
        using var client = broken.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = refresh
            ? await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = anterior })
            : await client.PostAsJsonAsync("/api/auth/login", new { Email = ua + "@test.invalid", Password = "Sesion123!" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("confidencial", await response.Content.ReadAsStringAsync());
        Assert.Equal(cantidad, await verificar.RefreshTokens.IgnoreQueryFilters().CountAsync());
        if (refresh)
        {
            var hashToken = new RefreshTokenHasher().Hash(anterior!);
            Assert.False((await verificar.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.Token == hashToken)).Revocado);
            Assert.Equal(HttpStatusCode.OK, (await normal.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = anterior })).StatusCode);
        }
        else Assert.Equal(HttpStatusCode.OK, (await normal.PostAsJsonAsync("/api/auth/login", new { Email = ua + "@test.invalid", Password = "Sesion123!" })).StatusCode);
    }

    [DatabaseFact] public Task H2_FalloJwtNoConsumeRefresh() => H2_FalloEmisionAsync(true);
    [DatabaseFact] public Task H2_FalloJwtNoCreaSesionLogin() => H2_FalloEmisionAsync(false);

    private async Task H2_FalloPersistenciaAsync(bool alCommit)
    {
        await PrepararLoginAsync();
        using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var anterior = (await LoginHttpAsync(client)).GetProperty("refreshToken").GetString()!;
        var hashToken = new RefreshTokenHasher().Hash(anterior);
        await using var db = Context(null);
        var cantidad = await db.RefreshTokens.IgnoreQueryFilters().CountAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION h2_rechazar_sesion() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'fallo-persistencia-sintetico-confidencial'; END; $$;
            """);
        await db.Database.ExecuteSqlRawAsync(alCommit
            ? "CREATE CONSTRAINT TRIGGER h2_rechazar AFTER INSERT ON \"RefreshToken\" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION h2_rechazar_sesion();"
            : "CREATE TRIGGER h2_rechazar BEFORE INSERT ON \"RefreshToken\" FOR EACH ROW EXECUTE FUNCTION h2_rechazar_sesion();");
        foreach (var refresh in new[] { true, false })
        {
            var response = refresh
                ? await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = anterior })
                : await client.PostAsJsonAsync("/api/auth/login", new { Email = ua + "@test.invalid", Password = "Sesion123!" });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("confidencial", body); Assert.DoesNotContain("accessToken", body);
            Assert.Equal(cantidad, await db.RefreshTokens.IgnoreQueryFilters().CountAsync());
            Assert.False((await db.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.Token == hashToken)).Revocado);
        }
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER h2_rechazar ON \"RefreshToken\"; DROP FUNCTION h2_rechazar_sesion();");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = anterior })).StatusCode);
    }

    [DatabaseFact] public Task H2_FalloInsercionRevierteSesion() => H2_FalloPersistenciaAsync(false);
    [DatabaseFact] public Task H2_FalloCommitRevierteSesionYNoDevuelveJwt() => H2_FalloPersistenciaAsync(true);

    [DatabaseFact]
    public async Task H2_EmisionConservaLocksDeIdentidadHastaConfirmar()
    {
        await using var db = Context(null);
        var nuevo = Guid.NewGuid().ToString("N");
        var result = await new AuthenticationTokenWriter(db).ConfirmarRefreshAsync(hash, nuevo, DateTime.UtcNow.AddHours(1), identidad =>
        {
            using var peer = new NpgsqlConnection(connection); peer.Open();
            foreach (var table in new[] { "Institucion", "Usuario" })
            {
                using var query = new NpgsqlCommand($"SELECT \"Id\" FROM \"{table}\" WHERE \"Id\" = @id FOR UPDATE NOWAIT", peer);
                query.Parameters.AddWithValue("id", table == "Institucion" ? a : ua);
                var error = Assert.Throws<PostgresException>(() => query.ExecuteScalar());
                Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
            }
            return identidad.Rol;
        });
        Assert.Equal("Docente", result);
        Assert.True(await db.RefreshTokens.IgnoreQueryFilters().AnyAsync(t => t.Token == nuevo && !t.Revocado));
    }
}
