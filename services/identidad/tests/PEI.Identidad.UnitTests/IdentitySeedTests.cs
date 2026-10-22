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
        Assert.Equal("08000000-0000-4000-8000-000000000001", SeedCatalog.Institutions[0].Id.ToString());
        foreach (var (institution, index) in SeedCatalog.Institutions.Select((i, n) => (i, n)))
        {
            var users = SeedCatalog.Users.Where(u => u.InstitucionId == institution.Id).ToArray();
            Assert.Equal(new[] { IdentityRoles.Administrador, IdentityRoles.Docente, IdentityRoles.Alumno, IdentityRoles.Alumno }, users.Select(u => u.Rol));
            Assert.Equal(new[] { "admin", "docente", "alumno1", "alumno2" }.Select(n => $"{n}@{(index == 0 ? "a" : "b")}.test"), users.Select(u => u.Email));
            for (var n = 0; n < users.Length; n++)
                Assert.Equal($"0800000{index + 1}-0000-4000-8000-{n + 1:D12}", users[n].Id.ToString());
        }
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

    private static SeedOptions Options(string environment, bool enabled, string schema, string host, int port, string database, bool authorized) => new()
    {
        EnvironmentName = environment, Enabled = enabled, Schema = schema, Operator = "operador-unit",
        ConnectionString = $"Host={host};Port={port};Database={database}",
        AllowedDestinations = authorized ? [new(host, port, database, schema)] : []
    };
}
