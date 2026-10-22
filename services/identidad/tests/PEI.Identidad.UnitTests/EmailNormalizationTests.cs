using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.UnitTests;

public sealed class EmailNormalizationTests
{
    [Theory]
    [InlineData("  Profesor@GMAIL.com  ", "Profesor@GMAIL.com")]
    [InlineData("\t\r\nDocente@escuela.edu.ar\n\t", "Docente@escuela.edu.ar")]
    [InlineData("\u00a0\u2003Persona@proveedor.example\u3000", "Persona@proveedor.example")]
    [InlineData("nombre.apellido+curso@otro-proveedor.org", "nombre.apellido+curso@otro-proveedor.org")]
    public void EmailRecortaBordesSinRestringirProveedorNiAlterarAlias(string entrada, string esperado)
    {
        var usuario = new Usuario { Email = entrada };
        Assert.Equal(esperado, usuario.Email);
        usuario.Email = "  Otro@Dominio.Propio  ";
        Assert.Equal("Otro@Dominio.Propio", usuario.Email);
    }

    [Fact]
    public void ClaveGlobalEsCalculadaPorPostgresYNoIncluyeInstitucion()
    {
        using var db = Context();
        var entidad = db.Model.FindEntityType(typeof(Usuario))!;
        Assert.Equal("pei_normalize_email(\"Email\")", entidad.FindProperty("EmailNormalizado")!.GetComputedColumnSql());
        Assert.True(entidad.FindProperty("EmailNormalizado")!.GetIsStored());
        var indice = Assert.Single(entidad.GetIndexes().Where(i => i.GetDatabaseName() == "IX_Usuario_EmailNormalizado"));
        Assert.True(indice.IsUnique);
        Assert.Equal("EmailNormalizado", Assert.Single(indice.Properties).Name);
    }

    [Fact]
    public void ParametroDeBusquedaUsaLaMismaFuncionSqlQueElIndice()
    {
        using var db = Context();
        var email = "  PROFESOR@gmail.com  ";
        var sql = db.Usuarios.Where(u => EF.Property<string>(u, "EmailNormalizado") == EmailDbFunctions.Normalize(email)).ToQueryString();
        Assert.Contains("pei_normalize_email(", sql);
        Assert.Contains("\"EmailNormalizado\"", sql);
        Assert.Contains("\"InstitucionId\"", sql); // el filtro multi-tenant se mantiene
    }

    [Fact]
    public void MigracionDetectaColisionesAntesDeTocarDatosYConservaProtecciones()
    {
        using var db = Context();
        var sql = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("LOCK TABLE \"Usuario\" IN ACCESS EXCLUSIVE MODE", sql);
        Assert.True(sql.IndexOf("HAVING count(*) > 1", StringComparison.Ordinal) <
            sql.IndexOf("UPDATE \"Usuario\" SET \"Email\"", StringComparison.Ordinal));
        Assert.Contains("CREATE UNIQUE INDEX \"IX_Usuario_EmailNormalizado\"", sql);
        Assert.Contains("CREATE TRIGGER pei_usuario_email_trim", sql);
        Assert.Contains("CREATE TRIGGER pei_usuario_invariants", sql);
        Assert.Contains("CREATE TRIGGER pei_token_invariants", sql);
    }

    private sealed record Tenant(Guid? InstitucionId) : ITenantContext;
    private static IdentidadDbContext Context() => new(
        new DbContextOptionsBuilder<IdentidadDbContext>().UseNpgsql("Host=localhost;Database=sin_conexion").Options,
        new Tenant(Guid.NewGuid()));
}
