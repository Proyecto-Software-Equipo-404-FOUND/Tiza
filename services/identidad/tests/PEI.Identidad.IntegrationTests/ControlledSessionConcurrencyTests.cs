using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    private string ConexionConNombre(string nombre) => new NpgsqlConnectionStringBuilder(connection) { ApplicationName = nombre }.ConnectionString;

    // No inferir concurrencia por Task.WhenAll: observar las sesiones realmente esperando locks.
    private static async Task EsperarBloqueadosAsync(NpgsqlConnection observer, string nombre, int cantidad, string? marcador = null)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            await using var cmd = new NpgsqlCommand("""
                SELECT count(*) FROM pg_stat_activity
                WHERE datname = current_database() AND application_name = @nombre
                  AND wait_event_type = 'Lock' AND cardinality(pg_blocking_pids(pid)) > 0
                  AND (query LIKE '%identidad:rotar-refresh%' OR query LIKE '%identidad:revocar-refresh%')
                  AND (@marcador = '' OR query LIKE '%' || @marcador || '%')
                """, observer);
            cmd.Parameters.AddWithValue("nombre", nombre);
            cmd.Parameters.AddWithValue("marcador", marcador ?? "");
            if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) >= cantidad) return;
            await Task.Delay(25);
        }
        Assert.Fail($"No se observaron {cantidad} solicitudes esperando locks de PostgreSQL.");
    }

    private static async Task<DateTime> RelojPostgresAsync(NpgsqlConnection cn)
    {
        await using var cmd = new NpgsqlCommand("SELECT clock_timestamp()", cn);
        return (DateTime)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task BloquearAsync(NpgsqlConnection cn, NpgsqlTransaction tx, string tokenHash, bool propietario = false)
    {
        var sql = propietario
            ? "SELECT \"Id\" FROM \"Usuario\" WHERE \"Id\" = @id FOR UPDATE"
            : "SELECT \"Id\" FROM \"RefreshToken\" WHERE \"Token\" = @hash FOR UPDATE";
        await using var cmd = new NpgsqlCommand(sql, cn, tx);
        if (propietario) cmd.Parameters.AddWithValue("id", ua);
        else cmd.Parameters.AddWithValue("hash", tokenHash);
        Assert.NotNull(await cmd.ExecuteScalarAsync());
    }

    private async Task VencimientoDuranteEsperaAsync(bool propietario)
    {
        var nombre = schema + "_vence";
        using var factory = new PostgresHttpFactory(ConexionConNombre(nombre), schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await using var observer = new NpgsqlConnection(connection); await observer.OpenAsync();
        var raw = new RefreshTokenService().GenerateToken(); var hashToken = new RefreshTokenHasher().Hash(raw);
        DateTime vence;
        await using (var insertar = new NpgsqlCommand("""
            INSERT INTO "RefreshToken" ("Id","UsuarioId","Token","FechaExpiracion","Revocado")
            VALUES (@id,@usuario,@hash,clock_timestamp() + interval '5 seconds',FALSE)
            RETURNING "FechaExpiracion"
            """, observer))
        {
            insertar.Parameters.AddWithValue("id", Guid.NewGuid()); insertar.Parameters.AddWithValue("usuario", ua); insertar.Parameters.AddWithValue("hash", hashToken);
            vence = (DateTime)(await insertar.ExecuteScalarAsync())!;
        }
        await using var blocker = new NpgsqlConnection(connection); await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await BloquearAsync(blocker, tx, hashToken, propietario);
        var renovacion = client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = raw });
        await EsperarBloqueadosAsync(observer, nombre, 1, "identidad:rotar-refresh");
        Assert.False(renovacion.IsCompleted);
        Assert.True(await RelojPostgresAsync(observer) < vence, "La operación debe alcanzar el lock mientras el token todavía es válido.");
        var timer = Stopwatch.StartNew();
        while (await RelojPostgresAsync(observer) <= vence)
        {
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
            await Task.Delay(25);
        }
        await tx.CommitAsync();
        using var response = await renovacion;
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var verificar = Context(null);
        Assert.Equal(2, await verificar.RefreshTokens.IgnoreQueryFilters().CountAsync()); // Seed + token vencido, sin reemplazo.
        Assert.False((await verificar.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.Token == hashToken)).Revocado);
    }

    [DatabaseFact]
    public Task RefreshVenceMientrasEsperaLockDelPropietario() => VencimientoDuranteEsperaAsync(propietario: true);

    [DatabaseFact]
    public Task RefreshVenceMientrasEsperaLockDelToken() => VencimientoDuranteEsperaAsync(propietario: false);

    [DatabaseFact]
    public async Task RefreshSimultaneoConDosSolicitudesRealmenteBloqueadas()
    {
        await PrepararLoginAsync(); var nombre = schema + "_dos_refresh";
        using var factory = new PostgresHttpFactory(ConexionConNombre(nombre), schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var sesion = await LoginHttpAsync(client); var raw = sesion.GetProperty("refreshToken").GetString()!;
        var hashToken = new RefreshTokenHasher().Hash(raw);
        await using var observer = new NpgsqlConnection(connection); await observer.OpenAsync();
        await using var blocker = new NpgsqlConnection(connection); await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await BloquearAsync(blocker, tx, hashToken);
        var primera = client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = raw });
        await EsperarBloqueadosAsync(observer, nombre, 1);
        var segunda = client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = raw });
        await EsperarBloqueadosAsync(observer, nombre, 2);
        Assert.False(primera.IsCompleted); Assert.False(segunda.IsCompleted);
        await tx.CommitAsync();
        using var r1 = await primera; using var r2 = await segunda;
        Assert.Single(new[] { r1, r2 }.Where(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Single(new[] { r1, r2 }.Where(r => r.StatusCode == HttpStatusCode.Unauthorized));
        var json = await (r1.IsSuccessStatusCode ? r1 : r2).Content.ReadFromJsonAsync<JsonElement>();
        var reemplazo = new RefreshTokenHasher().Hash(json.GetProperty("refreshToken").GetString()!);
        await using var verificar = Context(null);
        Assert.Equal(3, await verificar.RefreshTokens.IgnoreQueryFilters().CountAsync()); // Seed, consumido y un solo reemplazo.
        Assert.True((await verificar.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.Token == hashToken)).Revocado);
        Assert.True(await verificar.RefreshTokens.IgnoreQueryFilters().AnyAsync(t => t.Token == reemplazo && t.UsuarioId == ua && !t.Revocado));
    }

    private async Task CarreraRefreshLogoutAsync(bool primeroLogout)
    {
        await PrepararLoginAsync(); var nombre = schema + "_refresh_logout";
        using var factory = new PostgresHttpFactory(ConexionConNombre(nombre), schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var sesion = await LoginHttpAsync(client); var raw = sesion.GetProperty("refreshToken").GetString()!;
        var hashToken = new RefreshTokenHasher().Hash(raw);
        await using var observer = new NpgsqlConnection(connection); await observer.OpenAsync();
        await using var blocker = new NpgsqlConnection(connection); await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        await BloquearAsync(blocker, tx, hashToken);
        var primera = client.PostAsJsonAsync(primeroLogout ? "/api/auth/logout" : "/api/auth/refresh", new { RefreshToken = raw });
        await EsperarBloqueadosAsync(observer, nombre, 1, primeroLogout ? "identidad:revocar-refresh" : "identidad:rotar-refresh");
        var segunda = client.PostAsJsonAsync(primeroLogout ? "/api/auth/refresh" : "/api/auth/logout", new { RefreshToken = raw });
        await EsperarBloqueadosAsync(observer, nombre, 2);
        Assert.False(primera.IsCompleted); Assert.False(segunda.IsCompleted);
        await tx.CommitAsync();
        using var r1 = await primera; using var r2 = await segunda;
        var logout = primeroLogout ? r1 : r2; var refresh = primeroLogout ? r2 : r1;
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(primeroLogout ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, refresh.StatusCode);
        await using var verificar = Context(null);
        Assert.True((await verificar.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.Token == hashToken)).Revocado);
        Assert.Equal(primeroLogout ? 2 : 3, await verificar.RefreshTokens.IgnoreQueryFilters().CountAsync());
        if (!primeroLogout)
        {
            // Se conserva la semántica aprobada: logout del anterior no revoca al descendiente.
            var result = await refresh.Content.ReadFromJsonAsync<JsonElement>(); var nuevo = result.GetProperty("refreshToken").GetString()!;
            Assert.True(await verificar.RefreshTokens.IgnoreQueryFilters().AnyAsync(t => t.Token == new RefreshTokenHasher().Hash(nuevo) && !t.Revocado));
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = nuevo })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = nuevo })).StatusCode);
        }
    }

    [DatabaseFact]
    public Task LogoutGanaCarreraControladaContraRefresh() => CarreraRefreshLogoutAsync(primeroLogout: true);

    [DatabaseFact]
    public Task RefreshGanaCarreraControladaContraLogout() => CarreraRefreshLogoutAsync(primeroLogout: false);
}
