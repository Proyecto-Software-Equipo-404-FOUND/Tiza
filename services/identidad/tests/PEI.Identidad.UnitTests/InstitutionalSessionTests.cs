using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Application.UseCases.Auth.Login;
using PEI.Identidad.Application.UseCases.Auth.Refresh;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.UnitTests;

public sealed class InstitutionalSessionTests
{
    private static readonly JwtOptions Jwt = new() { SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), Issuer = "session-test", Audience = "session-api", AccessTokenMinutes = 7 };
    private static InstitutionalSessionService Service(FakeIdentityStore store, IAuthenticationTokenWriter? writer = null)
    {
        var issuer = new JwtTokenService(Options.Create(Jwt));
        return new(new LoginHandler(store, new BCryptPasswordHasher(), issuer), writer ?? store, new RefreshTokenService(), new RefreshTokenHasher(), issuer);
    }
    private static LoginCommand Login(FakeIdentityStore store) => new() { Email = store.Admin.Email, Password = "Prueba123!" };

    private sealed class WriterConFallo(Exception error) : IAuthenticationTokenWriter
    {
        public Task<T> ConfirmarLoginAsync<T>(PEI.Identidad.Domain.Entities.Usuario autenticado, string hash, DateTime vence, Func<RefreshIdentity, T> emitir, CancellationToken ct = default) => Task.FromException<T>(error);
        public Task<T> ConfirmarRefreshAsync<T>(string anterior, string nuevo, DateTime vence, Func<RefreshIdentity, T> emitir, CancellationToken ct = default) => Task.FromException<T>(error);
        public Task CrearRefreshTokenAsync(Guid usuarioId, string hash, DateTime vence, CancellationToken ct = default) => Task.FromException(error);
        public Task<bool> RevocarRefreshTokenAsync(string hash, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> RotarRefreshTokenAsync(string anterior, string nuevo, DateTime vence, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task LoginIdentidadNoDisponibleSeTraduceAErrorDeCredenciales()
    {
        var store = new FakeIdentityStore();
        var service = Service(store, new WriterConFallo(new AuthenticationIdentityUnavailableException()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoginAsync(Login(store)));
    }

    [Fact]
    public async Task LoginConservaErrorTecnicoSinConvertirloEnCredenciales()
    {
        var store = new FakeIdentityStore(); var error = new InvalidOperationException("Fallo técnico de prueba");
        var service = Service(store, new WriterConFallo(error));
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoginAsync(Login(store))));
    }

    [Theory]
    [InlineData(true, HttpStatusCode.Unauthorized)]
    [InlineData(false, HttpStatusCode.InternalServerError)]
    public async Task LoginHttpDistingueIdentidadNoDisponibleDeFalloTecnico(bool identidadNoDisponible, HttpStatusCode esperado)
    {
        using var factory = new IdentityHttpFactory();
        Exception error = identidadNoDisponible ? new AuthenticationIdentityUnavailableException() : new InvalidOperationException("detalle-tecnico-confidencial");
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(s =>
        {
            s.RemoveAll<IAuthenticationTokenWriter>(); s.AddSingleton<IAuthenticationTokenWriter>(new WriterConFallo(error));
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/auth/login", Login(factory.Store));
        Assert.Equal(esperado, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("detalle-tecnico-confidencial", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SwaggerDescribeDtoCodigosYContenidoDeLasSesiones()
    {
        using var factory = new IdentityHttpFactory(hostEnvironment: "Development"); using var client = factory.Client();
        var document = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        foreach (var path in new[] { "/api/auth/login", "/api/auth/refresh" })
        {
            var operation = paths.GetProperty(path).GetProperty("post");
            var responses = operation.GetProperty("responses");
            Assert.Equal(new[] { "200", "400", "401", "415", "429", "500" }, responses.EnumerateObject().Select(p => p.Name).OrderBy(x => x));
            var success = responses.GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
            Assert.Equal("#/components/schemas/SessionResponse", success.GetProperty("$ref").GetString());
            foreach (var status in new[] { "401", "415", "429", "500" })
                Assert.Equal("#/components/schemas/ProblemDetails", responses.GetProperty(status).GetProperty("content")
                    .GetProperty("application/problem+json").GetProperty("schema").GetProperty("$ref").GetString());
            Assert.False(operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
        }
        var schema = document.GetProperty("components").GetProperty("schemas").GetProperty("SessionResponse");
        var properties = schema.GetProperty("properties");
        var expected = new[] { "accessToken", "refreshToken", "tokenType", "accessTokenExpiresAt", "refreshTokenExpiresAt" };
        Assert.Equal(expected.OrderBy(x => x), properties.EnumerateObject().Select(p => p.Name).OrderBy(x => x));
        Assert.Equal(expected.OrderBy(x => x), schema.GetProperty("required").EnumerateArray().Select(p => p.GetString()).OrderBy(x => x));
        foreach (var name in expected) Assert.Equal("string", properties.GetProperty(name).GetProperty("type").GetString());
        foreach (var name in new[] { "accessTokenExpiresAt", "refreshTokenExpiresAt" })
            Assert.Equal("date-time", properties.GetProperty(name).GetProperty("format").GetString());
        var logout = paths.GetProperty("/api/auth/logout").GetProperty("post");
        Assert.Equal(new[] { "204", "400", "415", "429", "500" }, logout.GetProperty("responses").EnumerateObject().Select(p => p.Name).OrderBy(x => x));
        var noContent = logout.GetProperty("responses").GetProperty("204");
        Assert.False(noContent.TryGetProperty("content", out var content) && content.EnumerateObject().Any());
        Assert.False(logout.TryGetProperty("security", out var logoutSecurity) && logoutSecurity.GetArrayLength() > 0);
    }

    [Fact]
    public async Task LoginDevuelveTokensVencimientosRealesYHashPersistido()
    {
        var store = new FakeIdentityStore(); var sessions = Service(store);
        var result = await sessions.LoginAsync(Login(store));
        var access = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal(access.ValidTo, result.AccessTokenExpiresAt);
        Assert.InRange(result.AccessTokenExpiresAt, DateTime.UtcNow.AddMinutes(6.9), DateTime.UtcNow.AddMinutes(7));
        Assert.InRange(result.RefreshTokenExpiresAt, DateTime.UtcNow.AddDays(6.99), DateTime.UtcNow.AddDays(7));
        Assert.Equal(64, Convert.FromBase64String(result.RefreshToken).Length);
        Assert.Null(await store.BuscarIdentidadParaRenovacionAsync(result.RefreshToken));
        Assert.NotNull(await store.BuscarIdentidadParaRenovacionAsync(new RefreshTokenHasher().Hash(result.RefreshToken)));
    }

    [Fact]
    public async Task RefreshRotaYRechazaReusoDelAnterior()
    {
        var store = new FakeIdentityStore(); var sessions = Service(store);
        var primero = await sessions.LoginAsync(Login(store));
        var siguiente = await sessions.RefreshAsync(new() { RefreshToken = primero.RefreshToken });
        Assert.NotEqual(primero.RefreshToken, siguiente.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sessions.RefreshAsync(new() { RefreshToken = primero.RefreshToken }));
        Assert.NotNull(await sessions.RefreshAsync(new() { RefreshToken = siguiente.RefreshToken }));
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("vencido")]
    [InlineData("revocado")]
    [InlineData("usuario-inactivo")]
    [InlineData("institucion-inactiva")]
    [InlineData("rol-global")]
    public async Task RefreshInvalidoNoEmiteSesion(string caso)
    {
        var store = new FakeIdentityStore(); var sessions = Service(store); var token = new RefreshTokenService().GenerateToken();
        var hash = new RefreshTokenHasher().Hash(token);
        if (caso != "inexistente") await store.CrearRefreshTokenAsync(store.Admin.Id, hash, caso == "vencido" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddDays(1));
        if (caso == "revocado") await store.RevocarRefreshTokenAsync(hash);
        if (caso == "usuario-inactivo") store.Admin.Activo = false;
        if (caso == "institucion-inactiva") store.InstitutionActive = false;
        if (caso == "rol-global") store.Admin.Rol = "AdministradorGlobal";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sessions.RefreshAsync(new() { RefreshToken = token }));
    }

    [Fact]
    public async Task LogoutIdempotenteRevocaInclusoInstitucionInactiva()
    {
        var store = new FakeIdentityStore(); var sessions = Service(store);
        var result = await sessions.LoginAsync(Login(store)); store.InstitutionActive = false;
        var logout = new LogoutHandler(store, new RefreshTokenHasher());
        await logout.HandleAsync(new() { RefreshToken = result.RefreshToken });
        await logout.HandleAsync(new() { RefreshToken = result.RefreshToken });
        await logout.HandleAsync(new() { RefreshToken = "inexistente" });
        await logout.HandleAsync(new()); store.InstitutionActive = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sessions.RefreshAsync(new() { RefreshToken = result.RefreshToken }));
    }

    [Fact]
    public async Task HttpLoginRefreshLogoutConservanContratoId04()
    {
        using var factory = new IdentityHttpFactory(); using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/auth/login", Login(factory.Store));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", json.GetProperty("tokenType").GetString());
        var token = json.GetProperty("refreshToken").GetString();
        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = token });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = token })).StatusCode);
        var siguiente = (await refreshed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = siguiente })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = siguiente })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = siguiente })).StatusCode);
    }

    [Theory]
    [InlineData("Administrador", "administrador")]
    [InlineData("Docente", "docente")]
    [InlineData("Alumno", "alumno")]
    public async Task HttpRolesInstitucionalesExigenRolCorrecto(string role, string endpoint)
    {
        using var factory = new IdentityHttpFactory(); factory.Store.Admin.Rol = role;
        using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/" + endpoint)).StatusCode);
        var otro = endpoint == "docente" ? "alumno" : "docente";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/" + otro)).StatusCode);
    }

    [Fact]
    public async Task SwaggerNoAsignaPermisosDePlataformaAOperacionesInstitucionales()
    {
        using var factory = new IdentityHttpFactory(hostEnvironment: "Development"); using var client = factory.Client();
        var json = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.True(json.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("PlatformBearer", out _));
        var paths = json.GetProperty("paths");
        Assert.Equal("Bearer", paths.GetProperty("/api/auth/register").GetProperty("post").GetProperty("security")[0].EnumerateObject().Single().Name);
        Assert.Equal("PlatformBearer", paths.GetProperty("/api/plataforma/instituciones").GetProperty("post").GetProperty("security")[0].EnumerateObject().Single().Name);
        foreach (var path in new[] { "/api/auth/login", "/api/auth/refresh", "/api/auth/logout", "/api/plataforma/auth/login", "/api/plataforma/invitaciones/activar" })
            Assert.False(paths.GetProperty(path).GetProperty("post").TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
    }

    [Fact]
    public async Task RefreshInstitucionalNoProduceIdentidadGlobalNiAceptaJwtComoRefresh()
    {
        using var factory = new PlatformHttpFactory(); using var client = factory.Client();
        var global = await client.PostAsJsonAsync("/api/plataforma/auth/login", new { Email = factory.Store.Cuenta.Email, Password = "Global123!" });
        Assert.Equal(HttpStatusCode.OK, global.StatusCode);
        var globalJson = await global.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(globalJson.TryGetProperty("refreshToken", out _));
        var rawGlobal = globalJson.GetProperty("accessToken").GetString();
        var rechazo = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = rawGlobal });
        Assert.Contains(rechazo.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized });
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", rawGlobal);
        foreach (var role in new[] { "administrador", "docente", "alumno" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/" + role)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        var institucional = await client.PostAsJsonAsync("/api/auth/login", new { Email = factory.InstitucionalStore.Admin.Email, Password = "Prueba123!" });
        Assert.Equal(HttpStatusCode.OK, institucional.StatusCode);
        var tokens = await institucional.Content.ReadFromJsonAsync<JsonElement>();
        var renovacion = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = tokens.GetProperty("refreshToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, renovacion.StatusCode);
        var nuevo = await renovacion.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(nuevo.GetProperty("accessToken").GetString());
        Assert.Contains(jwt.Claims, c => c.Type == "institucion_id" && c.Value == factory.InstitucionalStore.Admin.InstitucionId.ToString());
        Assert.DoesNotContain(jwt.Claims, c => c.Value == "AdministradorGlobal");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", nuevo.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
    }
}
