using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Plataforma;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.UnitTests;

public sealed class PlatformHttpFactory : WebApplicationFactory<Program>
{
    public JwtOptions Institucional { get; } = new() { SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), Issuer = "institucional-test", Audience = "institucional-api" };
    public PlataformaOptions Plataforma { get; } = new() { Enabled = true, ActivationUrl = "https://localhost/activar",
        KeyDirectory = Path.Combine(Path.GetTempPath(), "tiza-test-keys", Guid.NewGuid().ToString("N")),
        DevelopmentMailDirectory = Path.Combine(Path.GetTempPath(), "tiza-test-mail", Guid.NewGuid().ToString("N")),
        Jwt = new() { SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), Issuer = "platform-test", Audience = "platform-api" } };
    public FakePlatformStore Store { get; } = new();
    public FakeIdentityStore InstitucionalStore { get; } = new();
    private Dictionary<string, string?> Config() => new()
    {
        ["ConnectionStrings:IdentidadDb"] = "Host=127.0.0.1;Port=1;Database=not_used",
        ["Jwt:SecretKey"] = Institucional.SecretKey, ["Jwt:Issuer"] = Institucional.Issuer, ["Jwt:Audience"] = Institucional.Audience,
        ["Platform:Enabled"] = "true", ["Platform:Jwt:SecretKey"] = Plataforma.Jwt.SecretKey,
        ["Platform:Jwt:Issuer"] = Plataforma.Jwt.Issuer, ["Platform:Jwt:Audience"] = Plataforma.Jwt.Audience,
        ["Platform:ActivationUrl"] = Plataforma.ActivationUrl, ["Platform:KeyDirectory"] = Plataforma.KeyDirectory,
        ["Platform:DevelopmentMailDirectory"] = Plataforma.DevelopmentMailDirectory, ["DevelopmentTools:EnableTestTokens"] = "false",
        ["LoginRateLimit:PermitLimit"] = "1000"
    };
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Config())); return base.CreateHost(builder);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(s =>
        {
            foreach (var descriptor in s.Where(x => x.ImplementationType == typeof(InvitacionesWorker)).ToList()) s.Remove(descriptor);
            s.RemoveAll<IPlataformaDataAccess>(); s.AddSingleton<IPlataformaDataAccess>(Store);
            s.RemoveAll<IAuthenticatedUserValidator>(); s.AddSingleton<IAuthenticatedUserValidator>(InstitucionalStore);
            s.RemoveAll<IAuthenticationDataAccess>(); s.AddSingleton<IAuthenticationDataAccess>(InstitucionalStore);
            s.RemoveAll<IAuthenticationTokenWriter>(); s.AddSingleton<IAuthenticationTokenWriter>(InstitucionalStore);
            s.AddControllers().AddApplicationPart(typeof(TenantProbeController).Assembly);
        });
    }
    public HttpClient Client(string? token = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    public string Token() => new PlataformaTokenService(Plataforma).Emitir(Store.Cuenta);
}

public sealed class FakePlatformStore : IPlataformaDataAccess
{
    public CuentaPlataforma Cuenta { get; } = new() { Email = "global@tiza.test", PasswordHash = new BCryptPasswordHasher().Hash("Global123!"), Nombre = "Global", Apellido = "Prueba" };
    public int Altas { get; private set; }
    public Task<CuentaPlataforma?> BuscarLoginAsync(string email, CancellationToken ct) => Task.FromResult<CuentaPlataforma?>(
        Cuenta.Activo && string.Equals(Cuenta.Email, email.Trim(), StringComparison.OrdinalIgnoreCase) ? Cuenta : null);
    public Task<bool> EstaActivaAsync(Guid id, CancellationToken ct) => Task.FromResult(Cuenta.Id == id && Cuenta.Activo);
    public Task<InstitucionCreada> CrearInstitucionAsync(Guid actor, CrearInstitucionCommand command, CancellationToken ct)
    { Assert.Equal(Cuenta.Id, actor); Altas++; return Task.FromResult(new InstitucionCreada(Guid.NewGuid(), Guid.NewGuid())); }
    public Task<IReadOnlyList<InstitucionResumen>> ListarInstitucionesAsync(Guid actor, int pagina, CancellationToken ct) => Task.FromResult<IReadOnlyList<InstitucionResumen>>(Array.Empty<InstitucionResumen>());
    public Task ActivarAsync(string token, string passwordHash, CancellationToken ct) => throw new UnauthorizedAccessException();
}

public sealed class PlataformaHttpTests
{
    [Fact]
    public async Task AdministracionGlobalAnonimaRechazada()
    {
        using var factory = new PlatformHttpFactory(); using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/plataforma/instituciones", new CrearInstitucionCommand())).StatusCode);
    }

    [Fact]
    public async Task LoginGlobalValidoEmiteIdentidadSinInstitucion()
    {
        using var factory = new PlatformHttpFactory(); using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/plataforma/auth/login", new { Email = "GLOBAL@tiza.test", Password = "Global123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(json.GetProperty("accessToken").GetString());
        Assert.DoesNotContain(token.Claims, c => c.Type == "institucion_id");
        Assert.Contains(token.Claims, c => c.Type == "identity_type" && c.Value == "platform");
    }

    [Theory]
    [InlineData("global@tiza.test", "incorrecta")]
    [InlineData("inexistente@tiza.test", "Global123!")]
    public async Task LoginGlobalInvalidoEsGenerico(string email, string password)
    {
        using var factory = new PlatformHttpFactory(); using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/plataforma/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(email, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task JwtGlobalRechazadoPorAuthorizeInstitucionalYRegistro()
    {
        using var factory = new PlatformHttpFactory(); using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/test/tenant")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/register", new { Email = "otro@tiza.test", Password = "Prueba123!", Nombre = "A", Apellido = "B", Rol = "Alumno" })).StatusCode);
    }

    [Fact]
    public async Task JwtInstitucionalRechazadoPorPlataforma()
    {
        using var factory = new PlatformHttpFactory();
        var token = new JwtTokenService(Options.Create(factory.Institucional)).GenerateToken(factory.InstitucionalStore.Admin);
        using var client = factory.Client(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
    }

    [Fact]
    public async Task PlataformaExigeCuentaActivaActual()
    {
        using var factory = new PlatformHttpFactory(); var token = factory.Token(); factory.Store.Cuenta.Activo = false;
        using var client = factory.Client(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
    }

    [Fact]
    public async Task LoginGlobalInactivoRechazado()
    {
        using var factory = new PlatformHttpFactory(); factory.Store.Cuenta.Activo = false;
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/plataforma/auth/login", new { Email = factory.Store.Cuenta.Email, Password = "Global123!" })).StatusCode);
    }

    [Fact]
    public async Task AdministracionGlobalValidaYRechazaCamposDeEscalada()
    {
        using var factory = new PlatformHttpFactory(); using var client = factory.Client(factory.Token());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
        var command = new { Nombre = "Institución", Codigo = "ABC", EmailAdministrador = "admin@tiza.test", NombreAdministrador = "A", ApellidoAdministrador = "B" };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/plataforma/instituciones", command)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/plataforma/instituciones", new { command.Nombre, command.Codigo, command.EmailAdministrador, command.NombreAdministrador, command.ApellidoAdministrador, Rol = "AdministradorGlobal" })).StatusCode);
        Assert.Equal(1, factory.Store.Altas);
    }

    [Theory]
    [InlineData("institucion_id", "00000000-0000-0000-0000-000000000001")]
    [InlineData("role", "Administrador")]
    [InlineData("identity_type", "institutional")]
    [InlineData("sub", "invalido")]
    public async Task ContratoGlobalRechazaClaimsAmbiguos(string type, string value)
    {
        using var factory = new PlatformHttpFactory();
        var claims = new List<Claim> { new("sub", factory.Store.Cuenta.Id.ToString()), new("role", "AdministradorGlobal"), new("identity_type", "platform"), new(type, value) };
        var jwt = factory.Plataforma.Jwt;
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)), SecurityAlgorithms.HmacSha256)));
        using var client = factory.Client(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
    }

    [Fact]
    public void OpcionesNoPermitenCompartirConfianzaNiUrlInsegura()
    {
        using var factory = new PlatformHttpFactory();
        factory.Plataforma.Jwt.SecretKey = factory.Institucional.SecretKey;
        Assert.Throws<InvalidOperationException>(() => factory.Plataforma.Validate(factory.Institucional, true));
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("firma")]
    [InlineData("vencimiento")]
    [InlineData("algoritmo")]
    public async Task JwtGlobalValidaFirmaIssuerAudienceVigenciaYAlgoritmo(string caso)
    {
        using var factory = new PlatformHttpFactory(); var jwt = factory.Plataforma.Jwt;
        var claims = new[] { new Claim("sub", factory.Store.Cuenta.Id.ToString()), new Claim("role", "AdministradorGlobal"), new Claim("identity_type", "platform") };
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(caso == "issuer" ? "otro" : jwt.Issuer,
            caso == "audience" ? "otro" : jwt.Audience, claims, DateTime.UtcNow.AddHours(-2),
            caso == "vencimiento" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(1),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(caso == "firma" ? factory.Institucional.SecretKey : jwt.SecretKey)),
                caso == "algoritmo" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256)));
        using var client = factory.Client(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
    }
}
