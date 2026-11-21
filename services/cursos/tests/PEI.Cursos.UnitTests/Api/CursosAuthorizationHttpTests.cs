using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Cursos.Application.Common.DTOs;
using PEI.Cursos.Application.DTOs.Cohortes;
using PEI.Cursos.Application.DTOs.Cursos;
using PEI.Cursos.Application.DTOs.Matriculaciones;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Infrastructure.Persistence;
using Swashbuckle.AspNetCore.Swagger;

namespace PEI.Cursos.UnitTests.Api;

public sealed class CursosAuthorizationHttpTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Usuario = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid Curso = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Cohorte = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid Matricula = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private const string Cursos = "/api/v1/cursos";
    private static readonly string CursoPath = $"{Cursos}/{Curso}";
    private static readonly string Cohortes = $"{CursoPath}/cohortes";
    private static readonly string Matriculas = $"/api/v1/cohortes/{Cohorte}/matriculas";
    private static readonly string Validacion = $"/api/v1/cohortes/{Cohorte}/validacion";

    public static TheoryData<string, string, bool, int> Operaciones => new()
    {
        { "GET", Cursos, false, 200 },
        { "GET", CursoPath, false, 200 },
        { "POST", Cursos, true, 201 },
        { "PUT", CursoPath, true, 200 },
        { "DELETE", CursoPath, true, 204 },
        { "GET", Cohortes, false, 200 },
        { "POST", Cohortes, true, 201 },
        { "PUT", $"{Cohortes}/{Cohorte}", true, 200 },
        { "DELETE", $"{Cohortes}/{Cohorte}", true, 204 },
        { "GET", Validacion, false, 200 },
        { "GET", Matriculas, false, 200 },
        { "GET", $"{Matriculas}/{Matricula}", false, 200 },
        { "POST", Matriculas, false, 201 }
    };

    [Theory]
    [MemberData(nameof(Operaciones))]
    public async Task AplicaMatrizPorAccionAntesDeDespachar(string metodo, string ruta, bool soloAdmin, int exito)
    {
        using var factory = new CursosFactory();
        using var client = factory.CreateClient();
        foreach (var rol in new[] { null, "Alumno", "Docente", "Administrador" })
        {
            var esperado = rol switch
            {
                null => 401,
                "Alumno" => 403,
                "Docente" when soloAdmin => 403,
                _ => exito
            };
            var llamadas = factory.Llamadas;
            using var request = new HttpRequestMessage(new HttpMethod(metodo), ruta);
            if (rol is not null) request.Headers.Authorization = new("Bearer", factory.Token(rol, TenantA));
            if (metodo is "POST" or "PUT")
                request.Content = JsonContent.Create(new { nombre = "Prueba", area = "Datos", periodo = "2026-2C", docenteId = Usuario, alumnoId = Usuario });

            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal((HttpStatusCode)esperado, response.StatusCode);
            if (esperado is 401 or 403)
            {
                Assert.Empty(await response.Content.ReadAsByteArrayAsync());
                if (esperado == 401)
                    Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");
            }
            Assert.Equal(llamadas + (esperado < 400 ? 1 : 0), factory.Llamadas);
        }
    }

    [Fact]
    public async Task MatriculaActivaDeAlumno_ElAlumnoSoloSeConsultaASiMismo()
    {
        using var factory = new CursosFactory();
        using var client = factory.CreateClient();
        var propia = $"{Matriculas}/alumnos/{Usuario}";
        var ajena = $"{Matriculas}/alumnos/{Guid.NewGuid()}";

        using (var sinToken = await client.GetAsync(propia))
            Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("Alumno", TenantA));
        using (var deSiMismo = await client.GetAsync(propia))
            Assert.Equal(HttpStatusCode.OK, deSiMismo.StatusCode);
        var llamadas = factory.Llamadas;
        using (var deOtro = await client.GetAsync(ajena))
            Assert.Equal(HttpStatusCode.Forbidden, deOtro.StatusCode);
        Assert.Equal(llamadas, factory.Llamadas);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("Docente", TenantA));
        using (var docente = await client.GetAsync(propia))
            Assert.Equal(HttpStatusCode.OK, docente.StatusCode);
        using (var noMatriculado = await client.GetAsync(ajena))
            Assert.Equal(HttpStatusCode.NotFound, noMatriculado.StatusCode);
    }

    [Theory]
    [InlineData("firma", HttpStatusCode.Unauthorized)]
    [InlineData("vencido", HttpStatusCode.Unauthorized)]
    [InlineData("sin-rol", HttpStatusCode.Forbidden)]
    [InlineData("rol-desconocido", HttpStatusCode.Forbidden)]
    public async Task DistingueJwtInvalidoDeRolNoPermitido(string escenario, HttpStatusCode esperado)
    {
        using var factory = new CursosFactory();
        using var client = factory.CreateClient();
        var rol = escenario switch { "sin-rol" => null, "rol-desconocido" => "Otro", _ => "Administrador" };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(rol, TenantA, escenario == "firma", escenario == "vencido"));

        using var response = await client.GetAsync(Cursos);
        Assert.Equal(esperado, response.StatusCode);
        Assert.Equal(0, factory.Llamadas);
    }

    [Fact]
    public async Task RolPermitidoConservaTenantDelJwtYRespuesta404ParaRecursosAjenos()
    {
        using var factory = new CursosFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("Administrador", TenantB));
        client.DefaultRequestHeaders.Add("X-Institucion-Id", TenantA.ToString());

        // El mediador de prueba simula el repositorio filtrado; QA04 cubre ese filtro en PostgreSQL.
        foreach (var ruta in new[] { CursoPath, Cohortes, Validacion, Matriculas, $"{Matriculas}/{Matricula}" })
        {
            using var response = await client.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(TenantB, factory.UltimoTenant);
        }

        using var escritura = await client.PutAsJsonAsync(CursoPath, new { nombre = "Prueba", area = "Datos" });
        Assert.Equal(HttpStatusCode.NotFound, escritura.StatusCode);
        Assert.Equal(TenantB, factory.UltimoTenant);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token("Docente", TenantA));
        using var propia = await client.GetAsync(Validacion);
        Assert.Equal(HttpStatusCode.OK, propia.StatusCode);
        var dto = await propia.Content.ReadFromJsonAsync<CohorteValidacionDto>();
        Assert.NotNull(dto);
        Assert.Equal(Cohorte, dto.CohorteId);
        Assert.Equal(TenantA, dto.InstitucionId);
    }

    [Fact]
    public void SwaggerDocumentaRespuestasDeLasTreceAccionesSinModificarMateriales()
    {
        using var factory = new CursosFactory();
        var document = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var operations = document.Paths.Where(path => !path.Key.Contains("materiales", StringComparison.Ordinal))
            .SelectMany(path => path.Value.Operations.Select(operation => (path.Key, operation.Key, Operation: operation.Value)))
            .ToArray();

        Assert.Equal(16, operations.Length);
        foreach (var (path, verb, operation) in operations)
        {
            Assert.Empty(operation.Responses["401"].Content);
            Assert.Empty(operation.Responses["403"].Content);
            var roles = path.Contains("/alumnos/", StringComparison.Ordinal)
                ? "Administrador, Docente o el propio Alumno"
                : verb == OperationType.Get || path.Contains("matriculas", StringComparison.Ordinal)
                    ? "Administrador o Docente" : "Administrador";
            Assert.Equal($"Rol no autorizado. Roles permitidos: {roles}.", operation.Responses["403"].Description);
        }
        Assert.DoesNotContain("Roles permitidos:", document.Paths["/api/v1/cursos/{cursoId}/materiales"]
            .Operations[OperationType.Post].Responses["403"].Description);
    }

    private sealed class CursosFactory : WebApplicationFactory<Program>
    {
        private const string Issuer = "cursos-http-tests";
        private const string Audience = "cursos-tests";
        private readonly byte[] key = RandomNumberGenerator.GetBytes(64);
        private Dictionary<string, string?> Config => new()
        {
            ["Jwt:Issuer"] = Issuer,
            ["Jwt:Audience"] = Audience,
            ["Jwt:SecretKey"] = Convert.ToBase64String(key),
            ["ConnectionStrings:CursosDb"] = "Host=127.0.0.1;Port=1;Database=not_used"
        };

        public int Llamadas { get; private set; }
        public Guid? UltimoTenant { get; private set; }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(Config));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Development dispara el seeder en ApplicationStarted. Testing no lo registra.
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Config));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CursosDbContext>();
                services.RemoveAll<IMediator>();
                services.AddScoped<IMediator>(sp => new MediadorPrueba(this, sp.GetRequiredService<ITenantContext>()));
            });
        }

        public string Token(string? rol, Guid tenant, bool otraFirma = false, bool vencido = false)
        {
            var claims = new List<Claim> { new("sub", Usuario.ToString()), new("institucion_id", tenant.ToString()) };
            if (rol is not null) claims.Add(new Claim("role", rol));
            var signingKey = otraFirma ? RandomNumberGenerator.GetBytes(64) : key;
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(signingKey))),
                SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Audience, claims,
                now.AddMinutes(-5), vencido ? now.AddMinutes(-1) : now.AddMinutes(5), credentials));
        }

        private sealed class MediadorPrueba(CursosFactory factory, ITenantContext tenant) : IMediator
        {
            public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            {
                factory.Llamadas++;
                factory.UltimoTenant = tenant.InstitucionId;
                if (tenant.InstitucionId != TenantA)
                    return Task.FromResult(typeof(TResponse) == typeof(bool) ? (TResponse)(object)false : default!);

                var curso = new CursoDto(Curso, TenantA, "Curso", "Datos", true, DateTime.UnixEpoch);
                var cohorte = new CohorteDto(Cohorte, Curso, Usuario, "Comisión", "2026-2C", true, DateTime.UnixEpoch);
                var matricula = new MatriculacionDto(Matricula, TenantA, Cohorte, Usuario, DateTime.UnixEpoch, "Activa");
                object resultado = typeof(TResponse) switch
                {
                    var t when t == typeof(CursoDto) => curso,
                    var t when t == typeof(CohorteDto) => cohorte,
                    var t when t == typeof(MatriculacionDto) => matricula,
                    var t when t == typeof(ResultadoPaginadoDTO<CursoDto>) => new ResultadoPaginadoDTO<CursoDto>([curso], 1, 1, 10, 1),
                    var t when t == typeof(IReadOnlyList<CohorteDto>) => new[] { cohorte },
                    var t when t == typeof(IReadOnlyList<MatriculacionDto>) => new[] { matricula },
                    var t when t == typeof(CohorteValidacionDto) => new CohorteValidacionDto(Cohorte, TenantA, Curso, Usuario, "Comisión", true),
                    var t when t == typeof(bool) => true,
                    _ => throw new InvalidOperationException($"Solicitud inesperada: {typeof(TRequest).Name}")
                };
                return Task.FromResult((TResponse)resultado);
            }
        }
    }
}
