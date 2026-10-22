using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.FileProviders;
using PEI.Identidad.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using PEI.Identidad.Api.Security;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Exceptions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.UnitTests;

// Solo el host de pruebas publica este controlador; no pertenece al ensamblado de la API.
[ApiController, Authorize, Route("test/tenant")]
public sealed class TenantProbeController(ITenantContext tenant) : ControllerBase
{
    [HttpGet] public IActionResult Get() => Ok(new { institucion = tenant.InstitucionId, usuario = User.FindFirst("sub")?.Value });
}

public sealed class IdentityHttpFactory(int limit = 1000, string hostEnvironment = "Testing") : WebApplicationFactory<Program>
{
    public JwtOptions Jwt { get; } = new() { SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), Issuer = "tests", Audience = "tests-api", ClockSkewSeconds = 0 };
    public FakeIdentityStore Store { get; } = new();
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IdentidadDb"] = "Host=127.0.0.1;Port=1;Database=not_used",
            ["Jwt:SecretKey"] = Jwt.SecretKey,
            ["Jwt:Issuer"] = Jwt.Issuer,
            ["Jwt:Audience"] = Jwt.Audience,
            ["Jwt:ClockSkewSeconds"] = "0",
            ["LoginRateLimit:PermitLimit"] = limit.ToString(),
            ["DevelopmentTools:EnableTestTokens"] = "false"
        }));
        return base.CreateHost(builder);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(hostEnvironment);
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IdentidadDb"] = "Host=127.0.0.1;Port=1;Database=not_used",
            ["Jwt:SecretKey"] = Jwt.SecretKey,
            ["Jwt:Issuer"] = Jwt.Issuer,
            ["Jwt:Audience"] = Jwt.Audience,
            ["Jwt:ClockSkewSeconds"] = "0",
            ["LoginRateLimit:PermitLimit"] = limit.ToString(),
            ["DevelopmentTools:EnableTestTokens"] = "false"
        }));
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<IAuthenticationDataAccess>(); s.RemoveAll<IRegistrationDataAccess>(); s.RemoveAll<IAuthenticatedUserValidator>();
            s.AddSingleton<IAuthenticationDataAccess>(Store); s.AddSingleton<IRegistrationDataAccess>(Store); s.AddSingleton<IAuthenticatedUserValidator>(Store);
            s.RemoveAll<IAuthenticationTokenWriter>(); s.AddSingleton<IAuthenticationTokenWriter>(Store);
            s.AddControllers().AddApplicationPart(typeof(TenantProbeController).Assembly);
        });
    }
    public HttpClient Client(string? token = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    public string Token(Usuario? user = null) => new JwtTokenService(Options.Create(Jwt)).GenerateToken(user ?? Store.Admin);
}

public sealed class FakeIdentityStore : IAuthenticationDataAccess, IRegistrationDataAccess, IAuthenticatedUserValidator, IAuthenticationTokenWriter
{
    private static readonly string Hash = new BCryptPasswordHasher().Hash("Prueba123!");
    public Usuario Admin { get; } = new() { InstitucionId = Guid.NewGuid(), Email = "admin@example.org", Rol = IdentityRoles.Administrador, PasswordHash = Hash };
    public bool InstitutionActive { get; set; } = true;
    public ConcurrentDictionary<Guid, Usuario> Users { get; } = new();
    public FakeIdentityStore() { Users[Admin.Id] = Admin; }
    public Task<Usuario?> BuscarUsuarioParaLoginAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Values.FirstOrDefault(u => u.Activo && InstitutionActive && string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));
    public Task<RefreshToken?> BuscarRefreshTokenAsync(string hash, CancellationToken cancellationToken = default) => Task.FromResult<RefreshToken?>(null);
    private sealed record Session(Guid Usuario, DateTime Expira, bool Revocado);
    private readonly ConcurrentDictionary<string, Session> sessions = new();
    private readonly object sessionLock = new();
    public Task<RefreshIdentity?> BuscarIdentidadParaRenovacionAsync(string hash, CancellationToken cancellationToken = default) =>
        Task.FromResult(sessions.TryGetValue(hash, out var t) && !t.Revocado && t.Expira > DateTime.UtcNow &&
            InstitutionActive && Users.TryGetValue(t.Usuario, out var u) && u.Activo
            ? new RefreshIdentity(u.Id, u.InstitucionId, u.Email, u.Rol) : null);
    public Task<T> ConfirmarLoginAsync<T>(Usuario autenticado, string nuevo, DateTime vence,
        Func<RefreshIdentity, T> emitir, CancellationToken ct = default)
    {
        lock (sessionLock)
        {
            if (!InstitutionActive || !Users.TryGetValue(autenticado.Id, out var u) || !u.Activo ||
                u.InstitucionId != autenticado.InstitucionId || u.PasswordHash != autenticado.PasswordHash || u.Email != autenticado.Email)
                throw new AuthenticationIdentityUnavailableException();
            var result = emitir(new(u.Id, u.InstitucionId, u.Email, u.Rol));
            if (!sessions.TryAdd(nuevo, new(u.Id, vence, false))) throw new InvalidOperationException();
            return Task.FromResult(result);
        }
    }
    public Task<T> ConfirmarRefreshAsync<T>(string anterior, string nuevo, DateTime vence,
        Func<RefreshIdentity, T> emitir, CancellationToken ct = default)
    {
        lock (sessionLock)
        {
            if (!sessions.TryGetValue(anterior, out var t) || t.Revocado || t.Expira <= DateTime.UtcNow ||
                !InstitutionActive || !Users.TryGetValue(t.Usuario, out var u) || !u.Activo)
                throw new AuthenticationIdentityUnavailableException();
            var result = emitir(new(u.Id, u.InstitucionId, u.Email, u.Rol));
            if (sessions.ContainsKey(nuevo)) throw new InvalidOperationException();
            sessions[anterior] = t with { Revocado = true }; sessions[nuevo] = new(t.Usuario, vence, false);
            return Task.FromResult(result);
        }
    }
    public Task CrearRefreshTokenAsync(Guid usuarioId, string hash, DateTime expira, CancellationToken cancellationToken = default)
    {
        if (!InstitutionActive || !Users.TryGetValue(usuarioId, out var u) || !u.Activo)
            throw new AuthenticationIdentityUnavailableException();
        if (!sessions.TryAdd(hash, new(usuarioId, expira, false))) throw new InvalidOperationException();
        return Task.CompletedTask;
    }
    public Task<bool> RevocarRefreshTokenAsync(string hash, CancellationToken cancellationToken = default)
    {
        lock (sessionLock)
        {
            if (!sessions.TryGetValue(hash, out var t) || t.Revocado) return Task.FromResult(false);
            sessions[hash] = t with { Revocado = true }; return Task.FromResult(true);
        }
    }
    public Task<bool> RotarRefreshTokenAsync(string anterior, string nuevo, DateTime expira, CancellationToken cancellationToken = default)
    {
        lock (sessionLock)
        {
            if (!sessions.TryGetValue(anterior, out var t) || t.Revocado || t.Expira <= DateTime.UtcNow ||
                !InstitutionActive || !Users.TryGetValue(t.Usuario, out var u) || !u.Activo) return Task.FromResult(false);
            if (sessions.ContainsKey(nuevo)) throw new InvalidOperationException();
            sessions[anterior] = t with { Revocado = true }; sessions[nuevo] = new(t.Usuario, expira, false);
            return Task.FromResult(true);
        }
    }
    public Task<bool> PuedeRegistrarAsync(Guid institucion) => Task.FromResult(InstitutionActive && institucion == Admin.InstitucionId && Admin.Activo);
    public Task<bool> ExisteEmailAsync(string email) => Task.FromResult(Users.Values.Any(u => string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));
    public Task AddAsync(Usuario usuario) { Users[usuario.Id] = usuario; return Task.CompletedTask; }
    public Task SaveChangesAsync() => Task.CompletedTask;
    public Task<bool> IsActiveAsync(Guid usuario, Guid institucion, string role, CancellationToken cancellationToken) =>
        Task.FromResult(InstitutionActive && Users.TryGetValue(usuario, out var u) && u.Activo && u.InstitucionId == institucion && u.Rol == role);
}

public sealed class AuthenticationHttpTests
{
    private static object Registro(string role = "Docente", string email = "nuevo@example.org") => new
    { Email = email, Password = "Prueba123!", Nombre = "Docente", Apellido = "Prueba", Rol = role };

    [Fact]
    public async Task LoginSinTenantYJwtCanonicoValidadoPorHttp()
    {
        using var factory = new IdentityHttpFactory(); using var anon = factory.Client();
        var response = await anon.PostAsJsonAsync("/api/auth/login", new { Email = "ADMIN@example.org", Password = "Prueba123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("accessToken").GetString()!;
        var payload = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(factory.Store.Admin.InstitucionId.ToString(), payload.Claims.Single(c => c.Type == "institucion_id").Value);
        Assert.Contains(payload.Claims, c => c.Type == "sub"); Assert.Contains(payload.Claims, c => c.Type == "role");
        Assert.Contains(payload.Claims, c => c.Type == "jti"); Assert.Contains(payload.Claims, c => c.Type == "iat");
        using var authenticated = factory.Client(token);
        var probe = await authenticated.GetFromJsonAsync<JsonElement>("/test/tenant");
        Assert.Equal(factory.Store.Admin.InstitucionId, probe.GetProperty("institucion").GetGuid());
    }
    [Fact]
    public async Task RegistroAnonimoRechazado()
    {
        using var f = new IdentityHttpFactory(); using var c = f.Client();
        var r = await c.PostAsJsonAsync("/api/auth/register", Registro()); Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        Assert.True(r.Headers.Contains("X-Correlation-ID"));
    }
    [Fact]
    public async Task AdministradorRegistraSoloEnSuInstitucion()
    {
        using var f = new IdentityHttpFactory(); using var c = f.Client(f.Token());
        var r = await c.PostAsJsonAsync("/api/auth/register", Registro()); Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal(f.Store.Admin.InstitucionId, f.Store.Users.Values.Single(u => u.Rol == "Docente").InstitucionId);
    }
    [Fact]
    public async Task InstitucionEnviadaPorClienteSeRechaza()
    {
        using var f = new IdentityHttpFactory(); using var c = f.Client(f.Token());
        var r = await c.PostAsJsonAsync("/api/auth/register", new { InstitucionId = Guid.NewGuid(), Email = "nuevo@example.org", Password = "Prueba123!", Nombre = "Prueba", Apellido = "Prueba", Rol = "Alumno" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Single(f.Store.Users);
    }
    [Theory]
    [InlineData("Administrador")]
    [InlineData("Usuario")]
    [InlineData("docente")]
    public async Task RolNoAsignableRechazado(string role)
    {
        using var f = new IdentityHttpFactory(); using var c = f.Client(f.Token());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/auth/register", Registro(role))).StatusCode);
    }
    [Fact]
    public async Task DocenteNoPuedeRegistrar()
    {
        using var f = new IdentityHttpFactory(); var docente = new Usuario { InstitucionId = f.Store.Admin.InstitucionId, Rol = "Docente" }; f.Store.Users[docente.Id] = docente;
        using var c = f.Client(f.Token(docente));
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/auth/register", Registro())).StatusCode);
    }
    [Fact]
    public async Task EmailDuplicadoDevuelveConflictoGenerico()
    {
        using var f = new IdentityHttpFactory(); using var c = f.Client(f.Token());
        var r = await c.PostAsJsonAsync("/api/auth/register", Registro(email: "ADMIN@example.org"));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.DoesNotContain("admin@example.org", await r.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TokenDeUsuarioOInstitucionInactivosSeRechaza(bool institution)
    {
        using var f = new IdentityHttpFactory(); var token = f.Token();
        if (institution) f.Store.InstitutionActive = false; else f.Store.Admin.Activo = false;
        using var c = f.Client(token); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/test/tenant")).StatusCode);
    }
    [Theory]
    [InlineData("sub-ausente")]
    [InlineData("tenant-ausente")]
    [InlineData("role-ausente")]
    [InlineData("sub-invalido")]
    [InlineData("sub-vacio")]
    [InlineData("tenant-invalido")]
    [InlineData("tenant-vacio")]
    [InlineData("tenant-duplicado")]
    [InlineData("sub-duplicado")]
    [InlineData("alias-legacy")]
    [InlineData("role-duplicado")]
    [InlineData("role-desconocido")]
    [InlineData("firma")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expirado")]
    [InlineData("algoritmo")]
    public async Task JwtInvalidoSeRechaza(string caso)
    {
        using var f = new IdentityHttpFactory();
        var claims = new List<Claim> { new("sub", f.Store.Admin.Id.ToString()), new("institucion_id", f.Store.Admin.InstitucionId.ToString()), new("role", "Administrador") };
        if (caso == "sub-ausente") claims.RemoveAll(c => c.Type == "sub");
        if (caso == "tenant-ausente") claims.RemoveAll(c => c.Type == "institucion_id");
        if (caso == "role-ausente") claims.RemoveAll(c => c.Type == "role");
        if (caso == "sub-invalido") { claims.RemoveAt(0); claims.Add(new("sub", "invalido")); }
        if (caso == "sub-vacio") { claims.RemoveAt(0); claims.Add(new("sub", Guid.Empty.ToString())); }
        if (caso == "tenant-invalido") { claims.RemoveAt(1); claims.Add(new("institucion_id", "invalido")); }
        if (caso == "tenant-vacio") { claims.RemoveAt(1); claims.Add(new("institucion_id", Guid.Empty.ToString())); }
        if (caso == "tenant-duplicado") claims.Add(new("institucion_id", f.Store.Admin.InstitucionId.ToString()));
        if (caso == "sub-duplicado") claims.Add(new("sub", f.Store.Admin.Id.ToString()));
        if (caso == "alias-legacy") claims.Add(new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        if (caso == "role-duplicado") claims.Add(new("role", "Docente"));
        if (caso == "role-desconocido") { claims.RemoveAt(2); claims.Add(new("role", "Usuario")); }
        var key = caso == "firma" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)) : f.Jwt.SecretKey;
        var token = new JwtSecurityToken(caso == "issuer" ? "otro" : f.Jwt.Issuer, caso == "audience" ? "otra" : f.Jwt.Audience, claims,
            DateTime.UtcNow.AddMinutes(-10), caso == "expirado" ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), caso == "algoritmo" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        using var c = f.Client(new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/test/tenant")).StatusCode);
    }
    [Fact]
    public async Task LimiteDeLoginDevuelve429()
    {
        using var f = new IdentityHttpFactory(2); using var c = f.Client();
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { Email = "inexistente@example.org", Password = "Prueba123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await c.PostAsJsonAsync("/api/auth/login", new { Email = "inexistente@example.org", Password = "Prueba123!" })).StatusCode);
    }
    [Theory]
    [InlineData("Testing")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task GeneradorDePruebaNoTieneEndpoint(string environment)
    {
        using var f = new IdentityHttpFactory(hostEnvironment: environment); using var c = f.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/v1/tokens/prueba", new { })).StatusCode);
    }
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public void HerramientaLocalSoloFuncionaConHabilitacionEnDevelopment(string environment, bool enabled)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DevelopmentTools:EnableTestTokens"] = enabled.ToString() }).Build();
        Assert.Throws<InvalidOperationException>(() => DevelopmentTokenCommand.Generate(config, environment));
    }
    [Fact]
    public void BcryptRealVerificaYRechazaHashesInvalidos()
    {
        var hasher = new BCryptPasswordHasher(); var hash = hasher.Hash("Prueba123!");
        Assert.True(hasher.Verify("Prueba123!", hash)); Assert.False(hasher.Verify("Incorrecta123!", hash));
        Assert.False(hasher.Verify("Prueba123!", "hash-invalido"));
        Assert.Throws<ArgumentException>(() => hasher.Hash(new string('x', 73)));
    }

    [Fact]
    public void HerramientaLocalGeneraClaimsCanonicosConClaveSeparada()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DevelopmentTools:EnableTestTokens"] = "true",
            ["DevelopmentTools:Jwt:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["DevelopmentTools:Jwt:Issuer"] = "local-only",
            ["DevelopmentTools:Jwt:Audience"] = "local-api",
            ["DevelopmentTools:UsuarioId"] = Guid.NewGuid().ToString(),
            ["DevelopmentTools:InstitucionId"] = Guid.NewGuid().ToString(),
            ["DevelopmentTools:Rol"] = "Administrador",
            ["Jwt:Issuer"] = "ordinary"
        }).Build();
        var json = JsonSerializer.Deserialize<JsonElement>(DevelopmentTokenCommand.Generate(config, "Development"));
        var token = json.GetProperty("access_token").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("local-only", jwt.Issuer); Assert.Single(jwt.Claims.Where(c => c.Type == "institucion_id"));
        new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token,
            DevelopmentTokenCommand.ReadOptions(config).ValidationParameters(), out _);
    }
    [Theory]
    [InlineData("http://0.0.0.0:5101")]
    [InlineData("http://example.org:5101")]
    [InlineData("http://*:5101")]
    public void ConfianzaLocalNoPermiteBindingsPublicos(string url) =>
        Assert.Throws<InvalidOperationException>(() => DevelopmentTokenCommand.ValidateLocalBindings(new[] { url }));

    [Fact]
    public async Task DuplicadoDeOtraInstitucionNoRevelaPropietario()
    {
        using var f = new IdentityHttpFactory(); f.Store.Users[Guid.NewGuid()] = new Usuario { InstitucionId = Guid.NewGuid(), Email = "ajeno@example.org", Rol = "Alumno" };
        using var c = f.Client(f.Token()); var r = await c.PostAsJsonAsync("/api/auth/register", Registro(email: "AJENO@example.org"));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode); Assert.DoesNotContain("ajeno@example.org", await r.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("password")]
    [InlineData("inexistente")]
    [InlineData("hash")]
    [InlineData("inactivo")]
    public async Task CredencialesFallidasDevuelven401Generico(string caso)
    {
        using var f = new IdentityHttpFactory(); if (caso == "hash") f.Store.Admin.PasswordHash = "invalido";
        if (caso == "inactivo") f.Store.Admin.Activo = false;
        using var c = f.Client(); var r = await c.PostAsJsonAsync("/api/auth/login", new { Email = caso == "inexistente" ? "otro@example.org" : "admin@example.org", Password = caso == "password" ? "Incorrecta123!" : "Prueba123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode); Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        Assert.True(r.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public void EndpointKestrelPublicoNoPuedeOcultarseTrasUrlsLoopback()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["urls"] = "http://127.0.0.1:5101", ["Kestrel:Endpoints:Http:Url"] = "http://0.0.0.0:5101" }).Build();
        Assert.Throws<InvalidOperationException>(() => DevelopmentTokenCommand.ValidateConfiguredBindings(config));
    }

    private sealed class LocalEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name!;
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    [Fact]
    public async Task TokenLocalSoloSeAceptaConConfianzaExplicitaYAislada()
    {
        // Host manual sin proveedores de User Secrets: este caso nunca carga configuracion real del usuario.
        var store = new FakeIdentityStore();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["Jwt:Issuer"] = "ordinary", ["Jwt:Audience"] = "ordinary-api",
            ["DevelopmentTools:EnableTestTokens"] = "true", ["urls"] = "http://127.0.0.1:5101",
            ["DevelopmentTools:Jwt:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["DevelopmentTools:Jwt:Issuer"] = "local", ["DevelopmentTools:Jwt:Audience"] = "local-api",
            ["DevelopmentTools:UsuarioId"] = store.Admin.Id.ToString(),
            ["DevelopmentTools:InstitucionId"] = store.Admin.InstitucionId.ToString(),
            ["DevelopmentTools:Rol"] = "Administrador"
        }).Build();
        DevelopmentTokenCommand.ValidateConfiguredBindings(config);
        var token = JsonSerializer.Deserialize<JsonElement>(DevelopmentTokenCommand.Generate(config, "Development"))
            .GetProperty("access_token").GetString()!;
        TestServer Server() => new(new WebHostBuilder().ConfigureServices(services =>
        {
            services.AddRouting(); services.AddHttpContextAccessor();
            services.AddScoped<ITenantContext, TenantContext>();
            services.AddSingleton<IAuthenticatedUserValidator>(store);
            services.AddIdentityAuthentication(config, new LocalEnvironment());
            services.AddControllers().AddApplicationPart(typeof(TenantProbeController).Assembly);
        }).Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));
        using (var server = Server())
        using (var client = server.CreateClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test/tenant")).StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
            var response = await client.GetAsync("/test/tenant");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(401, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
        }
        config["DevelopmentTools:EnableTestTokens"] = "false";
        using var disabledServer = Server(); using var disabledClient = disabledServer.CreateClient();
        disabledClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await disabledClient.GetAsync("/test/tenant")).StatusCode);
    }

    [Fact]
    public async Task PropiedadesJsonDuplicadasEnJwtSeRechazanAunqueElParserLasColapse()
    {
        using var factory = new IdentityHttpFactory();
        var payload = JsonSerializer.Serialize(new
        {
            sub = factory.Store.Admin.Id.ToString(), institucion_id = factory.Store.Admin.InstitucionId.ToString(), role = "Administrador",
            iss = factory.Jwt.Issuer, aud = factory.Jwt.Audience, exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()
        });
        payload = payload.Replace("\"sub\":", $"\"sub\":\"{factory.Store.Admin.Id}\",\"sub\":", StringComparison.Ordinal);
        var header = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var message = header + "." + Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payload));
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(factory.Jwt.SecretKey), Encoding.ASCII.GetBytes(message));
        using var client = factory.Client(message + "." + Base64UrlEncoder.Encode(signature));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/test/tenant")).StatusCode);
    }
}
