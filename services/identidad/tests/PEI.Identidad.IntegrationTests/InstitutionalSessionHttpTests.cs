using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    private async Task PrepararLoginAsync(string role = "Docente")
    {
        var hash = new BCryptPasswordHasher().Hash("Sesion123!");
        await using var db = Context(a);
        await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, hash).SetProperty(u => u.Rol, role));
    }
    private async Task<JsonElement> LoginHttpAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { Email = ua + "@test.invalid", Password = "Sesion123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [DatabaseFact]
    public async Task SesionHttpRotacionConcurrenteUnGanadorYLogoutSinTenant()
    {
        await PrepararLoginAsync(); using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var login = await LoginHttpAsync(client); var anterior = login.GetProperty("refreshToken").GetString()!;
        var respuestas = await Task.WhenAll(client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = anterior }), client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = anterior }));
        Assert.Single(respuestas.Where(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Single(respuestas.Where(r => r.StatusCode == HttpStatusCode.Unauthorized));
        using var ganador = respuestas.Single(r => r.StatusCode == HttpStatusCode.OK);
        var nuevo = await ganador.Content.ReadFromJsonAsync<JsonElement>();
        var nuevoRefresh = nuevo.GetProperty("refreshToken").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(nuevo.GetProperty("accessToken").GetString());
        Assert.Equal(jwt.ValidTo, nuevo.GetProperty("accessTokenExpiresAt").GetDateTime());
        Assert.Contains(jwt.Claims, c => c.Type == "institucion_id" && c.Value == a.ToString());
        Assert.DoesNotContain(jwt.Claims, c => c.Value == "AdministradorGlobal");
        await using var db = Context(null);
        Assert.Empty(await db.RefreshTokens.ToListAsync());
        Assert.True(await db.RefreshTokens.IgnoreQueryFilters().AnyAsync(t => t.Token == new RefreshTokenHasher().Hash(anterior) && t.Revocado));
        Assert.False(await db.RefreshTokens.IgnoreQueryFilters().AnyAsync(t => t.Token == nuevoRefresh));
        Assert.True(await db.RefreshTokens.IgnoreQueryFilters().AnyAsync(t => t.Token == new RefreshTokenHasher().Hash(nuevoRefresh) && t.UsuarioId == ua && !t.Revocado));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = nuevoRefresh })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = nuevoRefresh })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = nuevoRefresh })).StatusCode);
        foreach (var response in respuestas) response.Dispose();
    }

    [DatabaseFact]
    public async Task SesionHttpInactividadImpideRefreshPeroPermiteLogout()
    {
        await PrepararLoginAsync(); using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        foreach (var institucion in new[] { false, true })
        {
            var login = await LoginHttpAsync(client); var refresh = login.GetProperty("refreshToken").GetString();
            await using var db = Context(a);
            if (institucion) await db.Instituciones.ExecuteUpdateAsync(s => s.SetProperty(i => i.Activo, false));
            else await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Activo, false));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refresh })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = refresh })).StatusCode);
            await db.Instituciones.ExecuteUpdateAsync(s => s.SetProperty(i => i.Activo, true));
            await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Activo, true));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refresh })).StatusCode);
        }
    }

    [DatabaseFact]
    public async Task SesionHttpRefreshTomaRolActualYNoConservaPermisosObsoletos()
    {
        await PrepararLoginAsync(); using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var login = await LoginHttpAsync(client);
        await using var db = Context(a);
        await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Rol, "Alumno"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/docente")).StatusCode);
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = login.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/alumno")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/docente")).StatusCode);
    }

    [DatabaseFact]
    public async Task SesionHttpTokenVencidoNoRenuevaYLogoutLoRevoca()
    {
        using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var raw = new RefreshTokenService().GenerateToken(); var hash = new RefreshTokenHasher().Hash(raw);
        await using var db = Context(null);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RefreshToken" ("Id","UsuarioId","Token","FechaExpiracion","Revocado")
            VALUES ({Guid.NewGuid()},{ua},{hash},{DateTime.UtcNow.AddMinutes(-1)},FALSE)
            """);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = raw })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = raw })).StatusCode);
        Assert.True(await db.RefreshTokens.IgnoreQueryFilters().AnyAsync(t => t.Token == hash && t.Revocado));
    }

    [DatabaseFact]
    public async Task SesionRegistroHttpRespetaCorreoHistoricoDePlataforma()
    {
        var global = await InicializarPlataformaAsync();
        await using (var db = PlatformContext())
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "CuentaPlataforma" SET "Email" = 'nuevo-global@tiza.test' WHERE "Id" = {global}""");
        await PrepararLoginAsync("Administrador"); using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var login = await LoginHttpAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        var registro = new { Email = " GLOBAL@TIZA.TEST ", Password = "Prueba123!", Nombre = "Otro", Apellido = "Usuario", Rol = "Alumno" };
        var conflicto = await client.PostAsJsonAsync("/api/auth/register", registro);
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.DoesNotContain("Plataforma", await conflicto.Content.ReadAsStringAsync());
    }
}
