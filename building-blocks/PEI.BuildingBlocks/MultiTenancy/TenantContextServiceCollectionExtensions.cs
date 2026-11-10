using Microsoft.Extensions.DependencyInjection;

namespace PEI.BuildingBlocks.MultiTenancy;

public static class TenantContextServiceCollectionExtensions
{
    public static IServiceCollection AddTenantContext(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider =>
            provider.GetRequiredService<TenantContext>());

        return services;
    }
}
