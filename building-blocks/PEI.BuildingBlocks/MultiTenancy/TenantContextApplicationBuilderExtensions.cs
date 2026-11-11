using Microsoft.AspNetCore.Builder;

namespace PEI.BuildingBlocks.MultiTenancy;

public static class TenantContextApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<TenantContextMiddleware>();
    }
}
