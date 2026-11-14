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
using PEI.Identidad.Infrastructure.Security;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.IntegrationTests;

public sealed partial class TenantIsolationTests
{
    private sealed class PlatformPostgresFactory(string connection, string schema, IDataProtectionProvider protection, bool platformEnabled = true) : WebApplicationFactory<Program>
    {
        public EmailPrueba Sender { get; } = new();
        public JwtOptions InstitutionalJwt { get; } = new() { SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)), Issuer = "pg-institution", Audience = "pg-institution-api", ClockSkewSeconds = 0 };
        private readonly string platformKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentidadDb"] = connection,
                ["Jwt:SecretKey"] = InstitutionalJwt.SecretKey, ["Jwt:Issuer"] = InstitutionalJwt.Issuer, ["Jwt:Audience"] = InstitutionalJwt.Audience,
                ["Platform:Enabled"] = platformEnabled.ToString(), ["Platform:Jwt:SecretKey"] = platformKey,
                ["Platform:Jwt:Issuer"] = "pg-platform", ["Platform:Jwt:Audience"] = "pg-platform-api",
                ["Platform:ActivationUrl"] = "https://tiza.test/activar",
                ["Institutional:ActivationUrl"] = "https://tiza.test/activar-alumno",
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
                if (platformEnabled)
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

    private async Task<HttpClient> ClienteAdministradorAsync(PlatformPostgresFactory factory, Guid usuarioId, Guid institucionId, string role = "Administrador")
    {
        await using (var db = Context(institucionId))
        {
            var usuario = await db.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Id == usuarioId);
            usuario.Rol = role;
            await db.SaveChangesAsync();
        }
        var client = factory.Client();
        var usuarioJwt = new PEI.Identidad.Domain.Entities.Usuario
        {
            Id = usuarioId, InstitucionId = institucionId, Rol = role, Email = usuarioId + "@test.invalid"
        };
        var accessToken = new PEI.Identidad.Infrastructure.Security.JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(factory.InstitutionalJwt)).GenerateToken(usuarioJwt);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task PrepararEsquemaPlataformaAsync()
    {
        await using var db = PlatformContext();
        await db.Database.MigrateAsync();
    }

    [DatabaseFact]
    public async Task ImportacionCsvCreaAlumnoInvitacionOutboxYUsaActivationUrl()
    {
        await PrepararEsquemaPlataformaAsync();
        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection, platformEnabled: false);
        using var client = await ClienteAdministradorAsync(factory, ua, a);
        var csv = "nombre,apellido,email\r\nAna,Pérez," + Guid.NewGuid() + "@test.invalid\r\n" +
                  "Duplicado,Archivo," + Guid.NewGuid() + "@test.invalid\r\n";
        var repeated = Guid.NewGuid() + "@test.invalid";
        csv += $"Repetido,Archivo,{repeated}\r\nOtro,Archivo,{repeated}\r\n";
        csv += $"Existente,OtraInstitucion,{ub}@test.invalid\r\n";
        csv += "Inválido,Email,no-es-email\r\n";
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "archivo", "alumnos.csv");

        var response = await client.PostAsync("/api/usuarios/alumnos/importacion", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, result.GetProperty("creados").GetInt32());
        Assert.Equal(2, result.GetProperty("omitidos").GetInt32());
        Assert.Equal(1, result.GetProperty("errores").GetArrayLength());
        Assert.Equal(7, result.GetProperty("errores")[0].GetProperty("fila").GetInt32());
        await using var identity = Context(a);
        var alumno = await identity.Usuarios.SingleAsync(u => u.Nombre == "Ana");
        Assert.Equal("Alumno", alumno.Rol);
        Assert.False(alumno.Activo);
        Assert.Equal("!pendiente-activacion", alumno.PasswordHash);
        await using var plataforma = PlatformContext();
        var invitacion = await plataforma.Set<InvitacionAdministrador>().SingleAsync(i => i.UsuarioId == alumno.Id);
        Assert.Null(invitacion.Utilizada);
        var entrega = await plataforma.Set<EntregaInvitacion>().SingleAsync(e => e.InvitacionId == invitacion.Id);
        var mensaje = factory.Sender.Enviados.FirstOrDefault(e => e.Id == entrega.Id);
        if (mensaje is null)
        {
            var protector = platformProtection.CreateProtector(PlataformaDataAccess.ProtectionPurpose);
            mensaje = new MensajeEmail(entrega.Id, entrega.Destinatario, protector.Unprotect(entrega.ContenidoProtegido));
        }
        Assert.Contains("https://tiza.test/activar-alumno#token=", mensaje.Contenido, StringComparison.Ordinal);
        await using var otherTenant = Context(b);
        Assert.False(await otherTenant.Usuarios.AnyAsync(u => u.Nombre == "Ana"));
    }

    [DatabaseFact]
    public async Task ImportacionCsvRechazaAnonimoYRolNoAdministrador()
    {
        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection, platformEnabled: false);
        var csv = "nombre,apellido,email\nAna,Pérez,ana-" + Guid.NewGuid() + "@test.invalid\n";
        using (var anonymous = factory.Client())
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "archivo", "alumnos.csv");
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/usuarios/alumnos/importacion", form)).StatusCode);
        }
        using var docente = await ClienteAdministradorAsync(factory, ua2, a, "Docente");
        using var deniedForm = new MultipartFormDataContent();
        deniedForm.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "archivo", "alumnos.csv");
        Assert.Equal(HttpStatusCode.Forbidden, (await docente.PostAsync("/api/usuarios/alumnos/importacion", deniedForm)).StatusCode);
        await using var db = Context(a);
        Assert.False(await db.Usuarios.AnyAsync(u => u.Email.Contains("ana-")));
    }

    [DatabaseFact]
    public async Task ImportacionCsvMalformadaNoEscribeYFalloDeOutboxRevierteSoloLaFila()
    {
        await PrepararEsquemaPlataformaAsync();
        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection, platformEnabled: false);
        using var client = await ClienteAdministradorAsync(factory, ua, a);
        using (var malformed = new MultipartFormDataContent())
        {
            malformed.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("nombre,apellido,email\n\"Ana,Perez,a@test.invalid\n")), "archivo", "mal.csv");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/usuarios/alumnos/importacion", malformed)).StatusCode);
        }

        var fallido = "fallo-" + Guid.NewGuid() + "@test.invalid";
        await using (var setup = PlatformContext())
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION prueba_fallo_outbox_importacion() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN IF NEW."Destinatario" LIKE 'fallo-%@test.invalid' THEN RAISE EXCEPTION 'fallo fila'; END IF; RETURN NEW; END; $$;
                CREATE TRIGGER prueba_fallo_outbox_importacion BEFORE INSERT ON "EntregaInvitacion"
                FOR EACH ROW EXECUTE FUNCTION prueba_fallo_outbox_importacion();
                """);
        var exitoso = "ok-" + Guid.NewGuid() + "@test.invalid";
        var csv = $"nombre,apellido,email\nFalla,Fila,{fallido}\nSigue,Procesando,{exitoso}\n";
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "archivo", "alumnos.csv");
        var response = await client.PostAsync("/api/usuarios/alumnos/importacion", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("creados").GetInt32());
        Assert.Equal(1, result.GetProperty("errores").GetArrayLength());
        await using var check = Context(a);
        Assert.False(await check.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == fallido));
        Assert.True(await check.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == exitoso));
        await using var platformCheck = PlatformContext();
        var usuarioBueno = await check.Usuarios.IgnoreQueryFilters().SingleAsync(u => u.Email == exitoso);
        Assert.Single(await platformCheck.Set<InvitacionAdministrador>().Where(i => i.UsuarioId == usuarioBueno.Id).ToListAsync());
        Assert.Single(await platformCheck.Set<EntregaInvitacion>().ToListAsync());
    }

    [DatabaseFact]
    public async Task ImportacionCsvRechazaLimitesAntesDeEscribir()
    {
        await PrepararEsquemaPlataformaAsync();
        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection, platformEnabled: false);
        using var client = await ClienteAdministradorAsync(factory, ua, a);
        var overRows = "nombre,apellido,email\n" + string.Concat(Enumerable.Range(0, 201).Select(i => $"N{i},A{i},u{i}@limits.test\n"));
        using (var formRows = new MultipartFormDataContent())
        {
            formRows.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(overRows)), "archivo", "muchos.csv");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/usuarios/alumnos/importacion", formRows)).StatusCode);
        }
        using (var formSize = new MultipartFormDataContent())
        {
            formSize.Add(new ByteArrayContent(new byte[1024 * 1024 + 1]), "archivo", "grande.csv");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/usuarios/alumnos/importacion", formSize)).StatusCode);
        }
        await using var db = Context(a);
        Assert.Equal(2, await db.Usuarios.CountAsync());
        await using var platform = PlatformContext();
        Assert.Empty(await platform.Set<InvitacionAdministrador>().ToListAsync());
        Assert.Empty(await platform.Set<EntregaInvitacion>().ToListAsync());
    }

    [DatabaseFact]
    public async Task ImportacionConcurrenteConMismoEmailCreaSoloUnAlumnoYOtraSolicitudLoOmite()
    {
        const long advisoryLock = 741928361;
        await PrepararEsquemaPlataformaAsync();
        using var factory = new PlatformPostgresFactory(connection, schema, platformProtection, platformEnabled: false);
        using var firstClient = await ClienteAdministradorAsync(factory, ua, a);
        using var secondClient = await ClienteAdministradorAsync(factory, ua, a);
        var email = "race-" + Guid.NewGuid() + "@test.invalid";
        var csv = $"nombre,apellido,email\nConcurrencia,Prueba,{email}\n";

        await using var gate = new Npgsql.NpgsqlConnection(connection);
        await gate.OpenAsync();
        await using (var acquire = new Npgsql.NpgsqlCommand("SELECT pg_advisory_lock(@key)", gate))
        {
            acquire.Parameters.AddWithValue("key", advisoryLock);
            await acquire.ExecuteNonQueryAsync();
        }
        await using (var setup = PlatformContext())
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION prueba_barrera_importacion() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN PERFORM pg_advisory_xact_lock(741928361); RETURN NEW; END; $$;
                CREATE TRIGGER prueba_barrera_importacion BEFORE INSERT ON "Usuario"
                FOR EACH ROW EXECUTE FUNCTION prueba_barrera_importacion();
                """);

        async Task<HttpResponseMessage> Importar(HttpClient client)
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "archivo", "alumnos.csv");
            return await client.PostAsync("/api/usuarios/alumnos/importacion", form);
        }

        var first = Importar(firstClient);
        var second = Importar(secondClient);
        try
        {
            var dosEnEspera = false;
            for (var intento = 0; intento < 250; intento++)
            {
                await using var waiters = new Npgsql.NpgsqlCommand("""
                    SELECT count(*)::int FROM pg_stat_activity
                    WHERE pid <> pg_backend_pid() AND wait_event_type = 'Lock'
                      AND query LIKE 'INSERT INTO "Usuario"%'
                    """, gate);
                if ((int)(await waiters.ExecuteScalarAsync() ?? 0) >= 2) { dosEnEspera = true; break; }
                await Task.Delay(20);
            }
            Assert.True(dosEnEspera, "Las dos solicitudes no alcanzaron simultáneamente el punto de inserción.");
        }
        finally
        {
            await using var release = new Npgsql.NpgsqlCommand("SELECT pg_advisory_unlock(@key)", gate);
            release.Parameters.AddWithValue("key", advisoryLock);
            await release.ExecuteNonQueryAsync();
        }

        using var response1 = await first;
        using var response2 = await second;
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var result1 = await response1.Content.ReadFromJsonAsync<JsonElement>();
        var result2 = await response2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result1.GetProperty("creados").GetInt32() + result2.GetProperty("creados").GetInt32());
        Assert.Equal(1, result1.GetProperty("omitidos").GetInt32() + result2.GetProperty("omitidos").GetInt32());
        await using var comprobar = Context(a);
        var alumno = await comprobar.Usuarios.SingleAsync(u => u.Email == email);
        await using var plataforma = PlatformContext();
        Assert.Single(await plataforma.Set<InvitacionAdministrador>().Where(i => i.UsuarioId == alumno.Id).ToListAsync());
        Assert.Single(await plataforma.Set<EntregaInvitacion>().Where(e => e.InvitacionId == plataforma.Set<InvitacionAdministrador>()
            .Where(i => i.UsuarioId == alumno.Id).Select(i => i.Id).Single()).ToListAsync());
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
        // Integración ID-04/ID-06: el administrador activado renueva y cierra su sesión institucional.
        var refresh = institucionalJson.GetProperty("refreshToken").GetString();
        var renovacion = await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, renovacion.StatusCode);
        var siguiente = await renovacion.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", siguiente.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/plataforma/instituciones")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { RefreshToken = siguiente.GetProperty("refreshToken").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = siguiente.GetProperty("refreshToken").GetString() })).StatusCode);
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
