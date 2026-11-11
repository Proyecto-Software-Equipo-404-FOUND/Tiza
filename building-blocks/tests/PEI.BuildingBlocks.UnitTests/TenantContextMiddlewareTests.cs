using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PEI.BuildingBlocks.MultiTenancy;

namespace PEI.BuildingBlocks.UnitTests;

public sealed class TenantContextMiddlewareTests
{
    private static readonly Guid CanonicalUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AliasUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CanonicalInstitutionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid AliasInstitutionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public async Task ClaimsCanonicosValidosPueblanElContexto()
    {
        var tenant = new TenantContext();

        await InvokeAsync(tenant, Authenticated(
            new Claim("sub", CanonicalUserId.ToString()),
            new Claim("institucion_id", CanonicalInstitutionId.ToString()),
            new Claim("role", "Administrador")));

        Assert.Equal(CanonicalUserId, tenant.UsuarioId);
        Assert.Equal(CanonicalInstitutionId, tenant.InstitucionId);
        Assert.Equal("Administrador", tenant.Rol);
        Assert.True(tenant.EstaAutenticado);
    }

    [Fact]
    public async Task ClaimsCanonicosValidosPrevalecenSobreAliases()
    {
        var tenant = new TenantContext();

        await InvokeAsync(tenant, Authenticated(
            new Claim("sub", CanonicalUserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, AliasUserId.ToString()),
            new Claim("institucion_id", CanonicalInstitutionId.ToString()),
            new Claim("InstitucionId", AliasInstitutionId.ToString()),
            new Claim("role", "Administrador"),
            new Claim(ClaimTypes.Role, "Docente")));

        Assert.Equal(CanonicalUserId, tenant.UsuarioId);
        Assert.Equal(CanonicalInstitutionId, tenant.InstitucionId);
        Assert.Equal("Administrador", tenant.Rol);
    }

    [Fact]
    public async Task ClaimsMapeadosSeUsanCuandoFaltanLosCanonicos()
    {
        var tenant = new TenantContext();

        await InvokeAsync(tenant, Authenticated(
            new Claim(ClaimTypes.NameIdentifier, AliasUserId.ToString()),
            new Claim("InstitucionId", AliasInstitutionId.ToString()),
            new Claim(ClaimTypes.Role, "Docente")));

        Assert.Equal(AliasUserId, tenant.UsuarioId);
        Assert.Equal(AliasInstitutionId, tenant.InstitucionId);
        Assert.Equal("Docente", tenant.Rol);
    }

    [Fact]
    public async Task CanonicoDuplicadoDejaNuloElCampoSinRecurrirAlAlias()
    {
        var tenant = new TenantContext();

        await InvokeAsync(tenant, Authenticated(
            new Claim("sub", CanonicalUserId.ToString()),
            new Claim("sub", AliasUserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, AliasUserId.ToString()),
            new Claim("institucion_id", CanonicalInstitutionId.ToString()),
            new Claim("institucion_id", AliasInstitutionId.ToString()),
            new Claim("InstitucionId", AliasInstitutionId.ToString()),
            new Claim("role", "Administrador"),
            new Claim("role", "Docente"),
            new Claim(ClaimTypes.Role, "Docente")));

        Assert.Null(tenant.UsuarioId);
        Assert.Null(tenant.InstitucionId);
        Assert.Null(tenant.Rol);
        Assert.False(tenant.EstaAutenticado);
    }

    [Fact]
    public async Task AliasDuplicadoSinCanonicoDejaNuloElCampo()
    {
        var tenant = new TenantContext();

        await InvokeAsync(tenant, Authenticated(
            new Claim(ClaimTypes.NameIdentifier, AliasUserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, CanonicalUserId.ToString()),
            new Claim("InstitucionId", AliasInstitutionId.ToString()),
            new Claim("InstitucionId", CanonicalInstitutionId.ToString()),
            new Claim(ClaimTypes.Role, "Docente"),
            new Claim(ClaimTypes.Role, "Administrador")));

        Assert.Null(tenant.UsuarioId);
        Assert.Null(tenant.InstitucionId);
        Assert.Null(tenant.Rol);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task ClaimCanonicoInvalidoNoRecurreAlAlias(string invalidValue)
    {
        var tenant = new TenantContext();

        await InvokeAsync(tenant, Authenticated(
            new Claim("sub", invalidValue),
            new Claim(ClaimTypes.NameIdentifier, AliasUserId.ToString()),
            new Claim("institucion_id", invalidValue),
            new Claim("InstitucionId", AliasInstitutionId.ToString())));

        Assert.Null(tenant.UsuarioId);
        Assert.Null(tenant.InstitucionId);
    }

    [Theory]
    [InlineData("sub", ClaimTypes.NameIdentifier)]
    [InlineData("institucion_id", "InstitucionId")]
    public async Task AliasGuidInvalidoSinClaimCanonicoDejaNuloElCampo(
        string canonicalType,
        string aliasType)
    {
        var tenant = new TenantContext();
        var claims = canonicalType == "sub"
            ? new[]
            {
                new Claim(aliasType, "not-a-guid"),
                new Claim("institucion_id", CanonicalInstitutionId.ToString())
            }
            : new[]
            {
                new Claim("sub", CanonicalUserId.ToString()),
                new Claim(aliasType, "not-a-guid")
            };

        await InvokeAsync(tenant, AuthenticatedPrincipal(claims));

        if (canonicalType == "sub")
        {
            Assert.Null(tenant.UsuarioId);
            Assert.Equal(CanonicalInstitutionId, tenant.InstitucionId);
        }
        else
        {
            Assert.Equal(CanonicalUserId, tenant.UsuarioId);
            Assert.Null(tenant.InstitucionId);
        }
    }

    [Fact]
    public async Task ClaimsAusentesNoProducenContexto()
    {
        var tenant = new TenantContext();

        await InvokeAsync(tenant, Authenticated());

        Assert.Null(tenant.UsuarioId);
        Assert.Null(tenant.InstitucionId);
        Assert.Null(tenant.Rol);
        Assert.False(tenant.EstaAutenticado);
    }

    [Fact]
    public async Task TokenSinInstitucionNoCreaInstitucionFicticia()
    {
        var tenant = new TenantContext();

        await InvokeAsync(tenant, Authenticated(
            new Claim("sub", CanonicalUserId.ToString()),
            new Claim("role", "AdministradorGlobal"),
            new Claim("identity_type", "platform")));

        Assert.Equal(CanonicalUserId, tenant.UsuarioId);
        Assert.Null(tenant.InstitucionId);
        Assert.Equal("AdministradorGlobal", tenant.Rol);
    }

    [Fact]
    public async Task IdentidadNoAutenticadaNoGeneraContexto()
    {
        var tenant = new TenantContext();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", CanonicalUserId.ToString()),
            new Claim("institucion_id", CanonicalInstitutionId.ToString()),
            new Claim("role", "Administrador")
        }));

        await InvokeAsync(tenant, principal);

        Assert.Null(tenant.UsuarioId);
        Assert.Null(tenant.InstitucionId);
        Assert.Null(tenant.Rol);
        Assert.False(tenant.EstaAutenticado);
    }

    [Fact]
    public async Task MiddlewareContinuaConElSiguienteComponente()
    {
        var nextCallCount = 0;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextCallCount++;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext(), new TenantContext());

        Assert.Equal(1, nextCallCount);
    }

    [Fact]
    public async Task ExtensionAgregaMiddlewareAlPipelineYContinuaAlSiguienteComponente()
    {
        var institutionId = CanonicalInstitutionId;
        var services = new ServiceCollection();
        services.AddTenantContext();

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var app = new ApplicationBuilder(root);
        var nextCalled = false;

        app.UseTenantContext();
        app.Run(context =>
        {
            nextCalled = true;
            var tenant = context.RequestServices.GetRequiredService<ITenantContext>();
            Assert.Equal(institutionId, tenant.InstitucionId);
            return Task.CompletedTask;
        });

        var pipeline = app.Build();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = Authenticated(
                new Claim("sub", CanonicalUserId.ToString()),
                new Claim("institucion_id", institutionId.ToString()),
                new Claim("role", "Administrador"))
        };

        await pipeline(httpContext);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task LimpiaValoresPreviosAntesDeProcesarIdentidadNoAutenticada()
    {
        var tenant = new TenantContext
        {
            UsuarioId = CanonicalUserId,
            InstitucionId = CanonicalInstitutionId,
            Rol = "Administrador"
        };

        await InvokeAsync(tenant, new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Null(tenant.UsuarioId);
        Assert.Null(tenant.InstitucionId);
        Assert.Null(tenant.Rol);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    private static Task InvokeAsync(TenantContext tenant, ClaimsPrincipal principal)
    {
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext { User = principal };
        return middleware.InvokeAsync(context, tenant);
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));
}
