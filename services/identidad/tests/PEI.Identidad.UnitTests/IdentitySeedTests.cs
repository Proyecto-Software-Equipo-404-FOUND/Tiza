using PEI.Identidad.DevelopmentSeed;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.UnitTests;

public sealed class IdentitySeedTests
{
    [Fact]
    public void CatalogoEstableConRolesCanonicosYRelaciones()
    {
        Assert.Equal(2, SeedCatalog.Institutions.Count);
        Assert.Equal(8, SeedCatalog.Users.Count);
        Assert.Equal(10, SeedCatalog.Institutions.Select(i => i.Id).Concat(SeedCatalog.Users.Select(u => u.Id)).Distinct().Count());
        Assert.Equal(new[] { "11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222" },
            SeedCatalog.Institutions.Select(i => i.Id.ToString()));
        foreach (var (institution, index) in SeedCatalog.Institutions.Select((i, n) => (i, n)))
        {
            var users = SeedCatalog.Users.Where(u => u.InstitucionId == institution.Id).ToArray();
            Assert.Equal(new[] { IdentityRoles.Administrador, IdentityRoles.Docente, IdentityRoles.Alumno, IdentityRoles.Alumno }, users.Select(u => u.Rol));
            Assert.Equal(new[] { "admin", "docente", "alumno1", "alumno2" }.Select(n => $"{n}@{(index == 0 ? "a" : "b")}.test"), users.Select(u => u.Email));
        }
    }

    // Contrato de los fixtures de Cursos/Evaluaciones en origin/main f1ef6b7.
    // Cada correspondencia incluye institución y posición nominal del fixture, no solo el rol.
    [Theory]
    [InlineData("docente@a.test", "11111111-0000-0000-0000-000000000002", "11111111-1111-1111-1111-111111111111", "Docente")]
    [InlineData("alumno1@a.test", "11111111-0000-0000-0000-000000000003", "11111111-1111-1111-1111-111111111111", "Alumno")]
    [InlineData("alumno2@a.test", "11111111-0000-0000-0000-000000000004", "11111111-1111-1111-1111-111111111111", "Alumno")]
    [InlineData("docente@b.test", "22222222-0000-0000-0000-000000000002", "22222222-2222-2222-2222-222222222222", "Docente")]
    [InlineData("alumno1@b.test", "22222222-0000-0000-0000-000000000003", "22222222-2222-2222-2222-222222222222", "Alumno")]
    [InlineData("alumno2@b.test", "22222222-0000-0000-0000-000000000004", "22222222-2222-2222-2222-222222222222", "Alumno")]
    [InlineData("admin@a.test", "08000001-0000-4000-8000-000000000001", "11111111-1111-1111-1111-111111111111", "Administrador")]
    [InlineData("admin@b.test", "08000002-0000-4000-8000-000000000001", "22222222-2222-2222-2222-222222222222", "Administrador")]
    public void IdentidadesCanonicasYAdministradoresSinCorrespondenciaConservados(string email, string id, string institution, string role)
    {
        var user = Assert.Single(SeedCatalog.Users, u => u.Email == email);
        Assert.Equal(Guid.Parse(id), user.Id);
        Assert.Equal(Guid.Parse(institution), user.InstitucionId);
        Assert.Equal(role, user.Rol);
        Assert.Equal("ID08-v2", SeedCatalog.Version);
    }

    [Theory]
    [InlineData("Production", true, "pei_test_unit", "localhost", 5541, "pei_identidad_tests", true)]
    [InlineData("Staging", true, "pei_test_unit", "localhost", 5541, "pei_identidad_tests", true)]
    [InlineData("Testing", false, "pei_test_unit", "localhost", 5541, "pei_identidad_tests", true)]
    [InlineData("Testing", true, "public", "localhost", 5541, "pei_identidad_tests", true)]
    [InlineData("Testing", true, "pg_catalog", "localhost", 5541, "pei_identidad_tests", true)]
    [InlineData("Testing", true, "pei_test_unit;bad", "localhost", 5541, "pei_identidad_tests", true)]
    [InlineData("Testing", true, "pei_test_unit", "remote", 5541, "pei_identidad_tests", true)]
    [InlineData("Testing", true, "pei_test_unit", "localhost", 5432, "pei_identidad_tests", true)]
    [InlineData("Testing", true, "pei_test_unit", "localhost", 5441, "pei_identidad_tests", true)]
    [InlineData("Testing", true, "pei_test_unit", "localhost", 5541, "development", true)]
    [InlineData("Testing", true, "pei_test_unit", "localhost", 5541, "pei_identidad_tests", false)]
    [InlineData("Development", true, "pei_test_unit", "localhost", 5541, "pei_identidad_tests", false)]
    public void ConfiguracionNoAutorizadaRechazada(string environment, bool enabled, string schema, string host, int port, string database, bool authorized)
    {
        var options = Options(environment, enabled, schema, host, port, database, authorized);
        Assert.Throws<SeedRejectedException>(() => options.Validate());
    }

    [Fact]
    public void ConfiguracionValidaNoRequiereCredencialesDeUsuariosExistentes()
    {
        var options = Options("Testing", true, "pei_test_unit", "localhost", 5541, "pei_identidad_tests", true);
        var connection = options.Validate();
        Assert.Equal("pei_test_unit", connection.SearchPath);
        Assert.False(connection.IncludeErrorDetail);
        Assert.False(connection.LogParameters);
        Assert.False(connection.Pooling);
        Assert.Empty(options.Passwords);
    }

    [Fact]
    public void PublicDevelopmentExactoConAutorizacionExplicita()
    {
        // Solo se valida configuración; no se abre una conexión a desarrollo.
        var options = Options("Development", true, "public", "localhost", 5441, "pei_identidad_db", true, allowPublic: true);
        var connection = options.Validate();
        Assert.Equal("public", connection.SearchPath);
        Assert.Equal("pei_identidad_db", connection.Database);
        Assert.Equal(5441, connection.Port);
        Assert.False(connection.Pooling);
        Assert.False(connection.IncludeErrorDetail);
        Assert.False(connection.LogParameters);
    }

    [Fact]
    public void DevelopmentDedicadoExigeMismoDestinoEstandarYListaExplicita()
    {
        var options = Options("Development", true, "pei_dev", "localhost", 5441, "pei_identidad_db", true);
        Assert.Equal("pei_dev", options.Validate().SearchPath);
    }

    [Theory]
    [InlineData("remote", 5441, "pei_identidad_db")]
    [InlineData("localhost", 5441, "produccion")]
    [InlineData("localhost", 5541, "pei_identidad_db")]
    [InlineData("localhost", 5441, "pei_identidad_tests")]
    public void EsquemaDedicadoNoEludeRestriccionesDeDevelopment(string host, int port, string database)
    {
        var options = Options("Development", true, "pei_dev", host, port, database, true);
        Assert.Throws<SeedRejectedException>(() => options.Validate());
    }

    [Theory]
    [InlineData("Development", true, "localhost", 5441, "pei_identidad_db", true, false, "operador")]
    [InlineData("Development", true, "localhost", 5441, "pei_identidad_db", false, true, "operador")]
    [InlineData("Development", false, "localhost", 5441, "pei_identidad_db", true, true, "operador")]
    [InlineData("Production", true, "localhost", 5441, "pei_identidad_db", true, true, "operador")]
    [InlineData("Staging", true, "localhost", 5441, "pei_identidad_db", true, true, "operador")]
    [InlineData("Testing", true, "localhost", 5441, "pei_identidad_db", true, true, "operador")]
    [InlineData("Testing", true, "localhost", 5541, "pei_identidad_tests", true, true, "operador")]
    [InlineData("Development", true, "remote", 5441, "pei_identidad_db", true, true, "operador")]
    [InlineData("Development", true, "127.0.0.1", 5441, "pei_identidad_db", true, true, "operador")]
    [InlineData("Development", true, "localhost", 5432, "pei_identidad_db", true, true, "operador")]
    [InlineData("Development", true, "localhost", 5541, "pei_identidad_db", true, true, "operador")]
    [InlineData("Development", true, "localhost", 5441, "otra_base", true, true, "operador")]
    [InlineData("Development", true, "localhost", 5441, "pei_identidad_db", true, true, "")]
    [InlineData("Development", true, "localhost", 5441, "pei_identidad_db", true, true, " \t ")]
    [InlineData("Development", true, "localhost", 5441, "pei_identidad_db", true, true, "operador\nno-valido")]
    public void PublicRechazaDestinoEntornoOHabilitacionIncorrectos(string environment, bool enabled, string host,
        int port, string database, bool authorized, bool allowPublic, string actor)
    {
        var options = Options(environment, enabled, "public", host, port, database, authorized, allowPublic, actor);
        Assert.Throws<SeedRejectedException>(() => options.Validate());
    }

    private static SeedOptions Options(string environment, bool enabled, string schema, string host, int port, string database,
        bool authorized, bool allowPublic = false, string actor = "operador-unit") => new()
    {
        EnvironmentName = environment, Enabled = enabled, Schema = schema, Operator = actor, AllowDevelopmentPublic = allowPublic,
        ConnectionString = $"Host={host};Port={port};Database={database}",
        AllowedDestinations = authorized ? [new(host, port, database, schema)] : []
    };
}
