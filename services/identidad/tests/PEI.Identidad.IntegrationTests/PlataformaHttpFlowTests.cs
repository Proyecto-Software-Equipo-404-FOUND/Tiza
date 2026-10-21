using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Plataforma;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    private sealed class PlatformPostgresFactory(string connection, string schema, IDataProtectionProvider protection) : WebApplicationFactory<Program>
    {
        public EmailPrueba Sender { get; } = new();
        private readonly string institutionalKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        private readonly string platformKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentidadDb"] = connection,
                ["Jwt:SecretKey"] = institutionalKey, ["Jwt:Issuer"] = "pg-institution", ["Jwt:Audience"] = "pg-institution-api",
                ["Platform:Enabled"] = "true", ["Platform:Jwt:SecretKey"] = platformKey,
                ["Platform:Jwt:Issuer"] = "pg-platform", ["Platform:Jwt:Audience"] = "pg-platform-api",
                ["Platform:ActivationUrl"] = "https://tiza.test/activar",
                ["Platform:KeyDirectory"] = Path.Combine(Path.GetTempPath(), "tiza-keys", Guid.NewGuid().ToString("N")),
                ["Platform:DevelopmentMailDirectory"] = Path.Combine(Path.GetTempPath(), "tiza-mails", Guid.NewGuid().ToString("N")),
                ["DevelopmentTools:EnableTestTokens"] = "false", ["LoginRateLimit:PermitLimit"] = "1000"
            }));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(s =>
            {
                foreach (var descriptor in s.Where(x => x.ImplementationType == typeof(InvitacionesWorker)).ToList()) s.Remove(descriptor);
                s.RemoveAll<DbContextOptions<IdentidadDbContext>>();
                s.AddDbContext<IdentidadDbContext>(o => o.UseNpgsql(connection, n => n.MigrationsHistoryTable("__EFMigrationsHistory", schema)));
                s.RemoveAll<DbContextOptions<PlataformaDbContext>>();
                s.AddDbContext<PlataformaDbContext>(o => o.UseNpgsql(connection, n => n.MigrationsHistoryTable("__EFMigrationsHistoryPlataforma", schema)));
                s.AddSingleton(protection); s.RemoveAll<IEmailSender>(); s.AddSingleton<IEmailSender>(Sender);
            });
        }
        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [DatabaseFact]
    public async Task PlataformaFlujoHttpRealLoginAltaEntregaActivacionYRegistroInstitucional()
    {
        await InicializarPlataformaAsync();
        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection);
        using var client = factory.Client();
        var login = await client.PostAsJsonAsync("/api/plataforma/auth/login", new { Email = " GLOBAL@tiza.test ", Password = "GlobalSegura123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>(); var global = json.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", global);
        var alta = await client.PostAsJsonAsync("/api/plataforma/instituciones", Alta());
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        Assert.DoesNotContain("token", await alta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var creada = await alta.Content.ReadFromJsonAsync<InstitucionCreada>(); Assert.NotNull(creada);
        var listado = await client.GetAsync("/api/plataforma/instituciones"); Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        Assert.DoesNotContain("password", await listado.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/register", new { Email = "docente@tiza.test", Password = "Prueba123!", Nombre = "D", Apellido = "P", Rol = "Docente" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { Email = "primer.admin@tiza.test", Password = "Activada123!" })).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<InvitacionesOutbox>().ProcesarUnaAsync(default));
        var token = Assert.Single(factory.Sender.Enviados).Contenido.Split("#token=")[1];
        var activacion = new { Token = token, Password = "Activada123!" };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/plataforma/invitaciones/activar", activacion)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/plataforma/invitaciones/activar", activacion)).StatusCode);
        var institucional = await client.PostAsJsonAsync("/api/auth/login", new { Email = "PRIMER.ADMIN@tiza.test", Password = "Activada123!" });
        Assert.Equal(HttpStatusCode.OK, institucional.StatusCode);
        var institucionalJson = await institucional.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", institucionalJson.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { Email = "docente@tiza.test", Password = "Prueba123!", Nombre = "D", Apellido = "P", Rol = "Docente" })).StatusCode);
        await using var comprobar = Context(creada.InstitucionId);
        Assert.Equal(2, await comprobar.Usuarios.CountAsync());
    }

    [DatabaseFact]
    public async Task PlataformaFalloTardioRevierteInstitucionUsuarioReservaInvitacionYEntrega()
    {
        var actor = await InicializarPlataformaAsync(); await using var db = PlatformContext();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION prueba_fallo_auditoria() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Fallo de prueba'; END; $$;
            CREATE TRIGGER prueba_fallo_auditoria BEFORE INSERT ON "AuditoriaPlataforma"
                FOR EACH ROW EXECUTE FUNCTION prueba_fallo_auditoria();
            """);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => PlatformData(db).CrearInstitucionAsync(actor, Alta(), default));
        await using var comprobar = PlatformContext();
        Assert.Empty(await comprobar.Set<PEI.Identidad.Domain.Entities.InvitacionAdministrador>().ToListAsync());
        Assert.Empty(await comprobar.Set<PEI.Identidad.Domain.Entities.EntregaInvitacion>().ToListAsync());
        Assert.Equal(4, await comprobar.RegistrosEmail.CountAsync());
        await using var instituciones = Context(null);
        Assert.Equal(2, await instituciones.Instituciones.IgnoreQueryFilters().CountAsync());
        Assert.Equal(3, await instituciones.Usuarios.IgnoreQueryFilters().CountAsync());
    }
}
