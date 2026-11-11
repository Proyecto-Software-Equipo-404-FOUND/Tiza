using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PEI.Identidad.Application.Abstractions;
using PEI.BuildingBlocks.MultiTenancy;
using PEI.Identidad.Infrastructure.Authentication;
using PEI.Identidad.Infrastructure.Persistence;
using PEI.Identidad.Infrastructure.Tenancy;
using SharedTenantContext = PEI.BuildingBlocks.MultiTenancy.TenantContext;
using IdentityTenantContext = PEI.Identidad.Application.Abstractions.ITenantContext;

namespace PEI.Identidad.UnitTests;

public sealed class TenantContextTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalido")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ClaimAusenteOInvalidoCierraAcceso(string? value)
    {
        var claims = value is null ? Array.Empty<Claim>() : new[] { new Claim("institucion_id", value) };
        var sharedContext = await SharedContext(claims, authenticated: true);

        Assert.Null(sharedContext.InstitucionId);
    }

    [Fact]
    public async Task ClaimDuplicadoSeRechaza()
    {
        var id = Guid.NewGuid().ToString();
        var sharedContext = await SharedContext(new[]
        {
            new Claim("institucion_id", id),
            new Claim("institucion_id", id)
        }, authenticated: true);

        Assert.Null(sharedContext.InstitucionId);
    }

    [Fact]
    public async Task IdentidadAutenticadaRequerida()
    {
        var id = Guid.NewGuid();
        var claims = new[] { new Claim("institucion_id", id.ToString()) };

        var unauthenticatedContext = await SharedContext(claims, authenticated: false);
        var authenticatedContext = await SharedContext(claims, authenticated: true);

        Assert.Null(unauthenticatedContext.InstitucionId);
        Assert.Equal(id, authenticatedContext.InstitucionId);
        Assert.Null(new SharedTenantContext().InstitucionId);
    }



    [Fact]
    public async Task WriterRechazaDatosAntesDeConectar()
    {
        await using var db = new IdentidadDbContext(
            new DbContextOptionsBuilder<IdentidadDbContext>().Options,
            new TenantContextAdapter(new SharedTenantContext()));

        var writer = new AuthenticationTokenWriter(db);
        foreach (var hash in new[] { "", "contiene espacios", new string('a', 256) })
            await Assert.ThrowsAsync<ArgumentException>(() => writer.CrearRefreshTokenAsync(Guid.NewGuid(), hash, DateTime.UtcNow.AddHours(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.CrearRefreshTokenAsync(Guid.NewGuid(), "hash", DateTime.SpecifyKind(DateTime.UtcNow.AddHours(1), DateTimeKind.Unspecified)));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.CrearRefreshTokenAsync(Guid.NewGuid(), "hash", DateTime.UtcNow.AddHours(-1)));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.RotarRefreshTokenAsync("hash", "hash", DateTime.UtcNow.AddHours(1)));
    }

    private static async Task<SharedTenantContext> SharedContext(Claim[] claims, bool authenticated)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null));
        var httpContext = new DefaultHttpContext { User = principal };
        var sharedContext = new SharedTenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, sharedContext);
        return sharedContext;
    }

    [Fact]
    public void MigracionesIncluyenProteccionSinCrearColumnaXmin()
    {
        using var db = new IdentidadDbContext(
            new DbContextOptionsBuilder<IdentidadDbContext>()
                .UseNpgsql("Host=localhost;Database=sin_conexion").Options,
            new TenantContextAdapter(new SharedTenantContext()));

        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TRIGGER pei_usuario_invariants", script);
        Assert.Contains("CREATE TRIGGER pei_token_invariants", script);
        Assert.DoesNotContain("\"xmin\" xid", db.Database.GenerateCreateScript());
        foreach (var entity in db.Model.GetEntityTypes())
            Assert.True(entity.FindProperty("xmin")!.IsConcurrencyToken);
    }

    [Fact]
    public void AdaptadorDelegaEnElContextoCompartidoSinCopiarEstado()
    {
        var shared = new SharedTenantContext { InstitucionId = Guid.NewGuid() };
        IdentityTenantContext adapter = new TenantContextAdapter(shared);

        Assert.Equal(shared.InstitucionId, adapter.InstitucionId);

        shared.InstitucionId = Guid.NewGuid();
        Assert.Equal(shared.InstitucionId, adapter.InstitucionId);
    }
}
