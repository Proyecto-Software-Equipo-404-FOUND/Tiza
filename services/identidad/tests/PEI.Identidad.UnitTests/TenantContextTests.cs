using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PEI.Identidad.Infrastructure.Authentication;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Tenancy;

namespace PEI.Identidad.UnitTests;

public sealed class TenantContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalido")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void ClaimAusenteOInvalidoCierraAcceso(string? value)
    {
        var claims = value is null ? Array.Empty<Claim>() : new[] { new Claim("institucion_id", value) };
        Assert.Null(Context(claims, true).InstitucionId);
    }

    [Fact]
    public void ClaimDuplicadoSeRechaza()
    {
        var id = Guid.NewGuid().ToString();
        Assert.Null(Context(new[] { new Claim("institucion_id", id), new Claim("institucion_id", id) }, true).InstitucionId);
    }

    [Fact]
    public void IdentidadAutenticadaRequerida()
    {
        var id = Guid.NewGuid(); var claims = new[] { new Claim("institucion_id", id.ToString()) };
        Assert.Null(Context(claims, false).InstitucionId);
        Assert.Equal(id, Context(claims, true).InstitucionId);
        Assert.Null(new TenantContext(new HttpContextAccessor { HttpContext = null }).InstitucionId);
    }

    [Fact]
    public async Task WriterRechazaDatosAntesDeConectar()
    {
        await using var db = new IdentidadDbContext(new DbContextOptionsBuilder<IdentidadDbContext>().Options,
            new TenantContext(new HttpContextAccessor()));
        var writer = new AuthenticationTokenWriter(db);
        foreach (var hash in new[] { "", "contiene espacios", new string('a', 256) })
            await Assert.ThrowsAsync<ArgumentException>(() => writer.CrearRefreshTokenAsync(Guid.NewGuid(), hash, DateTime.UtcNow.AddHours(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.CrearRefreshTokenAsync(Guid.NewGuid(), "hash", DateTime.SpecifyKind(DateTime.UtcNow.AddHours(1), DateTimeKind.Unspecified)));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.CrearRefreshTokenAsync(Guid.NewGuid(), "hash", DateTime.UtcNow.AddHours(-1)));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.RotarRefreshTokenAsync("hash", "hash", DateTime.UtcNow.AddHours(1)));
    }

    private static TenantContext Context(Claim[] claims, bool authenticated) => new(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null)) }
    });

    [Fact]
    public void MigracionesIncluyenProteccionSinCrearColumnaXmin()
    {
        using var db = new IdentidadDbContext(new DbContextOptionsBuilder<IdentidadDbContext>()
            .UseNpgsql("Host=localhost;Database=sin_conexion").Options,
            new TenantContext(new HttpContextAccessor { HttpContext = null }));
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TRIGGER pei_usuario_invariants", script);
        Assert.Contains("CREATE TRIGGER pei_token_invariants", script);
        Assert.DoesNotContain("\"xmin\" xid", db.Database.GenerateCreateScript());
        foreach (var entity in db.Model.GetEntityTypes())
            Assert.True(entity.FindProperty("xmin")!.IsConcurrencyToken);
    }
}
