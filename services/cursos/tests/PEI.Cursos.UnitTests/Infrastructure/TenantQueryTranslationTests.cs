using Microsoft.EntityFrameworkCore;
using PEI.Cursos.Infrastructure.Persistence;
using PEI.Cursos.UnitTests.Common;

namespace PEI.Cursos.UnitTests.Infrastructure;

public class TenantQueryTranslationTests
{
    // ToQueryString ejecuta parametrización/traducción Npgsql sin abrir una conexión.
    // Reproduce la evaluación anticipada de Nullable.Value aunque exista un && previo.
    [Theory]
    [InlineData("Cursos", false)]
    [InlineData("Cohortes", false)]
    [InlineData("Matriculaciones", false)]
    [InlineData("Cursos", true)]
    [InlineData("Cohortes", true)]
    [InlineData("Matriculaciones", true)]
    public void ConsultaSinTenantDebeTraducirseSinExcepcion(string entidad, bool sinContexto)
    {
        var options = new DbContextOptionsBuilder<CursosDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused").Options;
        using var db = new CursosDbContext(options, sinContexto ? null : new FakeTenantContext { InstitucionId = null });
        var sql = entidad switch
        {
            "Cursos" => db.Cursos.ToQueryString(),
            "Cohortes" => db.Cohortes.ToQueryString(),
            _ => db.Matriculaciones.ToQueryString()
        };
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }
}
