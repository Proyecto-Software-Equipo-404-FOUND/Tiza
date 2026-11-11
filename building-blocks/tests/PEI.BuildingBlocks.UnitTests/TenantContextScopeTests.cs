using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PEI.BuildingBlocks.MultiTenancy;

namespace PEI.BuildingBlocks.UnitTests;

public sealed class TenantContextScopeTests
{
    [Fact]
    public async Task DiferentesScopesMantienenContextosIndependientes()
    {
        var institutionA = Guid.NewGuid();
        var institutionB = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddTenantContext();

        using var root = services.BuildServiceProvider();
        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();

        var tenantA = scopeA.ServiceProvider.GetRequiredService<TenantContext>();
        var interfaceA = scopeA.ServiceProvider.GetRequiredService<ITenantContext>();
        var tenantB = scopeB.ServiceProvider.GetRequiredService<TenantContext>();

        Assert.Same(tenantA, interfaceA);
        Assert.NotSame(tenantA, tenantB);

        await InvokeAsync(tenantA, institutionA);
        await InvokeAsync(tenantB, institutionB);

        // Processing scope B must not overwrite the context from scope A.
        Assert.Equal(institutionA, tenantA.InstitucionId);
        Assert.Equal(institutionB, tenantB.InstitucionId);
    }

    private static Task InvokeAsync(TenantContext tenant, Guid institutionId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("institucion_id", institutionId.ToString()),
            new Claim("role", "Docente")
        }, authenticationType: "test"));
        var context = new DefaultHttpContext { User = principal };
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        return middleware.InvokeAsync(context, tenant);
    }
}
