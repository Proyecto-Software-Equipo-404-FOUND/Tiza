using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Security;

namespace PEI.Identidad.IntegrationTests;
public sealed partial class TenantIsolationTests
{
    private sealed class PostgresHttpFactory(string connection, string schema) : WebApplicationFactory<Program>
    {
        public JwtOptions Jwt { get; } = new() { SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), Issuer = "postgres-tests", Audience = "postgres-http", ClockSkewSeconds = 0 };
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentidadDb"] = connection,
                ["Jwt:SecretKey"] = Jwt.SecretKey,
                ["Jwt:Issuer"] = Jwt.Issuer,
                ["Jwt:Audience"] = Jwt.Audience,
                ["Jwt:ClockSkewSeconds"] = "0",
                ["DevelopmentTools:EnableTestTokens"] = "false"
            }));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<DbContextOptions<IdentidadDbContext>>();
                s.AddDbContext<IdentidadDbContext>(o => o.UseNpgsql(connection, n => n.MigrationsHistoryTable("__EFMigrationsHistory", schema)));
            });
        }
        public HttpClient Client(Usuario user)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtTokenService(Options.Create(Jwt)).GenerateToken(user));
            return client;
        }
    }
    [DatabaseFact]
    public async Task RegistroHttpConPostgresAisladoYUnicidadGlobal()
    {
        await using (var db = Context(a))
            await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Rol, "Administrador"));
        using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.Client(new Usuario { Id = ua, InstitucionId = a, Rol = "Administrador" });
        var email = Guid.NewGuid() + "@http.test";
        var registro = new { Email = email, Password = "Prueba123!", Nombre = "Registro", Apellido = "HTTP", Rol = "Alumno" };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", registro)).StatusCode);
        await using (var verificar = Context(a))
        {
            var creado = await verificar.Usuarios.SingleAsync(u => u.Email == email);
            Assert.Equal(a, creado.InstitucionId); Assert.Equal("Alumno", creado.Rol);
            Assert.True(new BCryptPasswordHasher().Verify("Prueba123!", creado.PasswordHash));
        }
        await using (var verificar = Context(b)) Assert.False(await verificar.Usuarios.AnyAsync(u => u.Email == email));
        var ajeno = new { InstitucionId = b, Email = "otro@http.test", Password = "Prueba123!", Nombre = "Otro", Apellido = "HTTP", Rol = "Docente" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", ajeno)).StatusCode);
        // El email de B tambien esta reservado globalmente, sin revelar su institucion.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", new { Email = ub.ToString() + "@test.invalid", Password = "Prueba123!", Nombre = "Otro", Apellido = "HTTP", Rol = "Docente" })).StatusCode);
    }
    [DatabaseFact]
    public async Task LoginHttpSinTenantConPostgresReal()
    {
        await using (var db = Context(a))
        {
            var user = await db.Usuarios.SingleAsync(u => u.Id == ua);
            user.PasswordHash = new BCryptPasswordHasher().Hash("Prueba123!");
            await db.SaveChangesAsync();
        }
        using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { Email = ua.ToString().ToUpperInvariant() + "@TEST.INVALID", Password = "Prueba123!" })).StatusCode);
    }

    [DatabaseFact]
    public async Task RegistroHttpConcurrenteConEmailDuplicadoDevuelve409()
    {
        await using (var db = Context(a))
            await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Rol, "Administrador"));
        using var factory = new PostgresHttpFactory(connection, schema);
        _ = factory.Server;
        var email = Guid.NewGuid() + "@concurrencia.test";
        async Task<HttpStatusCode> Registrar(string correo)
        {
            using var client = factory.Client(new Usuario { Id = ua, InstitucionId = a, Rol = "Administrador" });
            return (await client.PostAsJsonAsync("/api/auth/register", new
            { Email = correo, Password = "Prueba123!", Nombre = "Concurrente", Apellido = "Prueba", Rol = "Docente" })).StatusCode;
        }
        var results = await Task.WhenAll(Registrar(email), Registrar(email.ToUpperInvariant()));
        Assert.Single(results.Where(r => r == HttpStatusCode.Created));
        Assert.Single(results.Where(r => r == HttpStatusCode.Conflict));
        await using var verificar = Context(a);
        Assert.Single(await verificar.Usuarios.Where(u => EF.Property<string>(u, "EmailNormalizado") == email).ToListAsync());
    }
    [DatabaseFact]
    public async Task InstitucionInactivaNoPermiteRegistroHttp()
    {
        await using (var db = Context(a))
        {
            await db.Usuarios.Where(u => u.Id == ua).ExecuteUpdateAsync(s => s.SetProperty(u => u.Rol, "Administrador"));
            await db.Instituciones.ExecuteUpdateAsync(s => s.SetProperty(i => i.Activo, false));
        }
        using var factory = new PostgresHttpFactory(connection, schema);
        using var client = factory.Client(new Usuario { Id = ua, InstitucionId = a, Rol = "Administrador" });
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        { Email = "rechazado@http.test", Password = "Prueba123!", Nombre = "Inactivo", Apellido = "Prueba", Rol = "Alumno" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var verificar = Context(a);
        Assert.False(await verificar.Usuarios.AnyAsync(u => u.Email == "rechazado@http.test"));
    }
}
