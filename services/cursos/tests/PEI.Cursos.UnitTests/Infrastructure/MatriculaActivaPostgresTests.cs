using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using PEI.Cursos.Application.UseCases.Matriculaciones;
using PEI.Cursos.Application.UseCases.Matriculaciones.Commands;
using PEI.Cursos.Domain.Entities;
using PEI.Cursos.Domain.Enums;
using PEI.Cursos.Infrastructure.Persistence;
using PEI.Cursos.Infrastructure.Persistence.Repositories;
using PEI.Cursos.UnitTests.Common;

namespace PEI.Cursos.UnitTests.Infrastructure;

public sealed class CursosPostgresTheoryAttribute : TheoryAttribute
{
    public CursosPostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TIZA_CU04_TEST_CONNECTION")))
            Skip = "Requiere PostgreSQL descartable; ver la guía de S3-CU-04.";
    }
}

public sealed class MatriculaActivaPostgresTests
{
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid alumno = Guid.NewGuid();

    private sealed class BarreraGuardado : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            return result;
        }
    }

    [CursosPostgresTheory]
    [InlineData("Administrador")]
    public async Task DosAltasSimultaneasEnLaMismaCohorteConservanElConflictoHistorico(string rol)
    {
        var connection = Conexion();
        var schema = "cu04_" + Guid.NewGuid().ToString("N");
        connection.SearchPath = schema;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var setup = Context(connection);
            await setup.Database.MigrateAsync();
            var curso = new Curso(tenant, "Matemática", "Ciencias");
            var cohorte = new Cohorte(tenant, curso.Id, Guid.NewGuid(), "A", "2026");
            setup.AddRange(curso, cohorte);
            await setup.SaveChangesAsync();

            // Ambos requests deben superar las consultas preventivas antes de guardar.
            var barrier = new BarreraGuardado();
            using var api = new CursosApi(connection.ConnectionString, barrier);
            using var client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Authorization = new("Bearer", api.Token(rol, tenant, alumno));
            var ruta = $"/api/v1/cohortes/{cohorte.Id}/matriculas";
            var respuestas = await Task.WhenAll(
                client.PostAsJsonAsync(ruta, new { alumnoId = alumno }),
                client.PostAsJsonAsync(ruta, new { alumnoId = alumno }));
            try
            {
                Assert.Single(respuestas.Where(r => r.StatusCode == HttpStatusCode.Created));
                var conflicto = Assert.Single(respuestas.Where(r => r.StatusCode == HttpStatusCode.Conflict));
                Assert.Equal("application/problem+json", conflicto.Content.Headers.ContentType?.MediaType);
                var problem = await conflicto.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal(409, problem.GetProperty("status").GetInt32());
                Assert.Equal("Conflicto en la operación", problem.GetProperty("title").GetString());
                Assert.Equal("El alumno ya se encuentra matriculado en la cohorte.", problem.GetProperty("detail").GetString());
                Assert.True(!problem.TryGetProperty("type", out var type) || type.ValueKind == JsonValueKind.Null,
                    "El conflicto histórico no debe incluir un código de conflicto entre comisiones.");

                await using var verify = Context(connection);
                var matricula = Assert.Single(await verify.Matriculaciones.AsNoTracking().ToListAsync());
                Assert.Equal(tenant, matricula.InstitucionId);
                Assert.Equal(curso.Id, matricula.CursoId);
                Assert.Equal(cohorte.Id, matricula.CohorteId);
                Assert.Equal(alumno, matricula.AlumnoId);
                Assert.Equal(EstadoMatriculacion.Activa, matricula.Estado);
            }
            finally
            {
                foreach (var respuesta in respuestas) respuesta.Dispose();
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [CursosPostgresTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DosEscritoresQueSuperaronLaConsultaSoloConfirmanUnaActiva(bool reactivarA, bool reactivarB)
    {
        var connection = Conexion();
        var schema = "cu04_" + Guid.NewGuid().ToString("N");
        connection.SearchPath = schema;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin))
            await create.ExecuteNonQueryAsync();

        try
        {
            await using var setup = Context(connection);
            await setup.Database.MigrateAsync();
            var curso = new Curso(tenant, "Matemática", "Ciencias");
            var a = new Cohorte(tenant, curso.Id, Guid.NewGuid(), "A", "2026");
            var b = new Cohorte(tenant, curso.Id, Guid.NewGuid(), "B", "2026");
            setup.AddRange(curso, a, b);
            await setup.SaveChangesAsync();
            var anteriorA = await Historial(setup, a, reactivarA);
            var anteriorB = await Historial(setup, b, reactivarB);
            var barrier = new BarreraGuardado();
            var resultados = await Task.WhenAll(
                Operacion(connection, a.Id, anteriorA, barrier),
                Operacion(connection, b.Id, anteriorB, barrier));

            Assert.Single(resultados.Where(ok => ok));
            Assert.Single(resultados.Where(ok => !ok));
            await using var verify = Context(connection);
            var rows = await verify.Matriculaciones.AsNoTracking().ToListAsync();
            Assert.Single(rows.Where(m => m.Estado == EstadoMatriculacion.Activa));
            foreach (var id in new[] { anteriorA, anteriorB }.OfType<Guid>())
                Assert.Contains(rows, m => m.Id == id);
            Assert.Equal(new[] { anteriorA, anteriorB }.Count(id => id.HasValue)
                + (resultados[0] && !reactivarA || resultados[1] && !reactivarB ? 1 : 0), rows.Count);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [CursosPostgresTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task MigracionConservaHistorialORevierteSinCambiarDatos(bool duplicado, bool tenantIncorrecto)
    {
        var connection = Conexion();
        var schema = "cu04_" + Guid.NewGuid().ToString("N");
        connection.SearchPath = schema;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await Ejecutar($"CREATE SCHEMA {schema}");
        try
        {
            await using var db = Context(connection);
            await db.GetService<IMigrator>().MigrateAsync("20261106103500_AddMaterialCurso");
            var curso = Guid.NewGuid();
            await Ejecutar($"""
                INSERT INTO "Cursos" ("Id","InstitucionId","Nombre","Area","Activo","FechaCreacionUtc")
                VALUES ('{curso}','{tenant}','Prueba','Datos',true,now());
                """);
            for (var i = 0; i < 2; i++)
            {
                var cohorte = Guid.NewGuid();
                var institucionMatricula = tenantIncorrecto && i == 1 ? Guid.NewGuid() : tenant;
                var estado = i == 0 || duplicado ? "Activa" : "Baja";
                await Ejecutar($"""
                    INSERT INTO "Cohortes" ("Id","InstitucionId","CursoId","DocenteId","Nombre","Periodo","Activo","FechaCreacionUtc")
                    VALUES ('{cohorte}','{tenant}','{curso}','{Guid.NewGuid()}','Comisión {i}','2026',true,now());
                    INSERT INTO "Matriculaciones" ("Id","InstitucionId","CohorteId","AlumnoId","Estado","FechaAlta","FechaCreacionUtc")
                    VALUES ('{Guid.NewGuid()}','{institucionMatricula}','{cohorte}','{alumno}','{estado}',now(),now());
                    """);
            }

            if (duplicado || tenantIncorrecto)
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
                Assert.Contains(duplicado ? "varias matrículas activas" : "relaciones inconsistentes", error.MessageText);
                await using var column = new NpgsqlCommand("""
                    SELECT count(*) FROM information_schema.columns
                    WHERE table_schema = current_schema() AND table_name = 'Matriculaciones' AND column_name = 'CursoId'
                    """, admin);
                Assert.Equal(0L, await column.ExecuteScalarAsync());
            }
            else
            {
                await db.Database.MigrateAsync();
                var rows = await db.Matriculaciones.AsNoTracking().ToListAsync();
                Assert.All(rows, m => Assert.Equal(curso, m.CursoId));
                Assert.Single(rows.Where(m => m.Estado == EstadoMatriculacion.Baja));

                await using var desalineada = Context(connection);
                desalineada.Add(new Matriculacion(tenant, rows[0].CohorteId, Guid.NewGuid(), Guid.NewGuid()));
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => new UnitOfWork(desalineada).CommitAsync());
                Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
            }

            await using var count = new NpgsqlCommand("""SELECT count(*) FROM "Matriculaciones" """, admin);
            Assert.Equal(2L, await count.ExecuteScalarAsync());
        }
        finally
        {
            await Ejecutar($"DROP SCHEMA {schema} CASCADE");
        }

        async Task Ejecutar(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, admin);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    [CursosPostgresTheory]
    [InlineData("Administrador")]
    [InlineData("Docente")]
    public async Task HttpConPersistenciaRealRespetaReglaRolesTenantYHistorial(string rol)
    {
        var connection = Conexion();
        var schema = "cu04_" + Guid.NewGuid().ToString("N");
        connection.SearchPath = schema;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var setup = Context(connection);
            await setup.Database.MigrateAsync();
            var otroTenant = Guid.NewGuid();
            var curso = new Curso(tenant, "Matemática", "Ciencias");
            var otroCurso = new Curso(tenant, "Física", "Ciencias");
            var cursoAjeno = new Curso(otroTenant, "Química", "Ciencias");
            var a = new Cohorte(tenant, curso.Id, Guid.NewGuid(), "A", "2026");
            var b = new Cohorte(tenant, curso.Id, Guid.NewGuid(), "B", "2026");
            var c = new Cohorte(tenant, otroCurso.Id, Guid.NewGuid(), "C", "2026");
            var d = new Cohorte(otroTenant, cursoAjeno.Id, Guid.NewGuid(), "D", "2026");
            setup.AddRange(curso, otroCurso, cursoAjeno, a, b, c, d);
            await setup.SaveChangesAsync();

            using var api = new CursosApi(connection.ConnectionString);
            using var client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            static string Ruta(Guid cohorte) => $"/api/v1/cohortes/{cohorte}/matriculas";
            async Task<(HttpStatusCode Status, JsonElement? Body)> Alta(Guid cohorte, Guid alumnoId)
            {
                using var response = await client.PostAsJsonAsync(Ruta(cohorte), new { alumnoId });
                return (response.StatusCode, response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict
                    ? await response.Content.ReadFromJsonAsync<JsonElement>() : null);
            }
            void Token(string role, Guid institucion) => client.DefaultRequestHeaders.Authorization = new("Bearer", api.Token(role, institucion, alumno));

            Assert.Equal(HttpStatusCode.Unauthorized, (await Alta(a.Id, alumno)).Status);
            Token("Alumno", tenant);
            Assert.Equal(HttpStatusCode.Forbidden, (await Alta(a.Id, alumno)).Status);
            Token(rol, tenant);
            Assert.Equal(HttpStatusCode.NotFound, (await Alta(d.Id, alumno)).Status);
            var alta = await Alta(a.Id, alumno);
            Assert.Equal(HttpStatusCode.Created, alta.Status);
            var id = alta.Body!.Value.GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Conflict, (await Alta(a.Id, alumno)).Status);
            var otra = await Alta(b.Id, alumno);
            Assert.Equal(HttpStatusCode.Conflict, otra.Status);
            Assert.Equal("ALUMNO_EN_OTRA_COMISION", otra.Body!.Value.GetProperty("type").GetString());
            Assert.Equal(HttpStatusCode.Created, (await Alta(c.Id, alumno)).Status);
            Assert.Equal(HttpStatusCode.Created, (await Alta(b.Id, Guid.NewGuid())).Status);

            var detalle = $"{Ruta(a.Id)}/{id}";
            using (var baja = await client.DeleteAsync(detalle)) Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Alta(a.Id, alumno)).Status);
            using (var reactivar = await client.PostAsync(detalle + "/reactivar", null))
                Assert.Equal(HttpStatusCode.NoContent, reactivar.StatusCode);
            using (var baja = await client.DeleteAsync(detalle)) Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await Alta(b.Id, alumno)).Status);
            using (var conflicto = await client.PostAsync(detalle + "/reactivar", null))
            {
                Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
                Assert.Equal("application/problem+json", conflicto.Content.Headers.ContentType?.MediaType);
                Assert.Equal("ALUMNO_EN_OTRA_COMISION", (await conflicto.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
            }

            using (var historial = await client.GetAsync(detalle))
            {
                Assert.Equal(HttpStatusCode.OK, historial.StatusCode);
                Assert.Equal("Baja", (await historial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
            }

            Token("Alumno", tenant);
            using (var baja = await client.DeleteAsync(detalle)) Assert.Equal(HttpStatusCode.Forbidden, baja.StatusCode);
            using (var reactivar = await client.PostAsync(detalle + "/reactivar", null))
                Assert.Equal(HttpStatusCode.Forbidden, reactivar.StatusCode);
            using (var historial = await client.GetAsync($"{Ruta(b.Id)}/alumnos/{alumno}"))
            {
                Assert.Equal(HttpStatusCode.OK, historial.StatusCode);
                Assert.Equal("Activa", (await historial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetString());
            }

            Token(rol, otroTenant);
            using (var reactivar = await client.PostAsync(detalle + "/reactivar", null))
                Assert.Equal(HttpStatusCode.NotFound, reactivar.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await Alta(d.Id, alumno)).Status);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class CursosApi(string connection, BarreraGuardado? barrier = null) : WebApplicationFactory<Program>
    {
        private readonly string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        private Dictionary<string, string?> Settings => new()
        {
            ["Jwt:Issuer"] = "cu04-tests", ["Jwt:Audience"] = "cu04-tests",
            ["Jwt:SecretKey"] = key, ["ConnectionStrings:CursosDb"] = connection
        };

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(cfg => cfg.AddInMemoryCollection(Settings));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(Settings));
            if (barrier is not null)
                builder.ConfigureServices(services =>
                    services.AddDbContext<CursosDbContext>(options => options.AddInterceptors(barrier)));
        }

        public string Token(string role, Guid institucion, Guid usuario) =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("cu04-tests", "cu04-tests",
                [new Claim("sub", usuario.ToString()), new Claim("institucion_id", institucion.ToString()), new Claim("role", role)],
                DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddMinutes(2),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)));
    }

    private static NpgsqlConnectionStringBuilder Conexion()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TIZA_CU04_TEST_CONNECTION"));
        if (connection.Host != "127.0.0.1" || connection.Port is 5432 or >= 5441 and <= 5445 or 5541 ||
            connection.Database != "pei_cursos_cu04_tests" || connection.Username != "cu04_test")
            throw new InvalidOperationException("Usar exclusivamente una instancia loopback descartable para S3-CU-04.");
        connection.Pooling = false;
        connection.SearchPath = null;
        return connection;
    }

    private CursosDbContext Context(NpgsqlConnectionStringBuilder connection, BarreraGuardado? barrier = null)
    {
        var options = new DbContextOptionsBuilder<CursosDbContext>()
            .UseNpgsql(connection.ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", connection.SearchPath));
        if (barrier is not null) options.AddInterceptors(barrier);
        return new CursosDbContext(options.Options, new FakeTenantContext { InstitucionId = tenant });
    }

    private async Task<Guid?> Historial(CursosDbContext db, Cohorte cohorte, bool existe)
    {
        if (!existe) return null;
        var m = new Matriculacion(tenant, cohorte.Id, alumno, cohorte.CursoId);
        m.DarDeBaja();
        db.Add(m);
        await db.SaveChangesAsync();
        return m.Id;
    }

    private async Task<bool> Operacion(NpgsqlConnectionStringBuilder connection, Guid cohorteId, Guid? anterior, BarreraGuardado barrier)
    {
        await using var db = Context(connection, barrier);
        var repo = new MatriculacionRepository(db);
        var uow = new UnitOfWork(db);
        try
        {
            if (anterior is Guid id)
                Assert.True(await new ReactivarMatriculacionCommandHandler(repo, uow).HandleAsync(new(cohorteId, id)));
            else
                await new MatricularAlumnoCommandHandler(new CohorteRepository(db), repo, uow,
                    new FakeTenantContext { InstitucionId = tenant }).HandleAsync(new(cohorteId, alumno));
            return true;
        }
        catch (AlumnoEnOtraComisionException exception)
        {
            Assert.Equal("ALUMNO_EN_OTRA_COMISION", exception.Code);
            return false;
        }
    }
}
