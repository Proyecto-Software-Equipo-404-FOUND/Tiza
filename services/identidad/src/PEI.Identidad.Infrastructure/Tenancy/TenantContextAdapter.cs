using PEI.Identidad.Application.Abstractions;
using SharedTenantContext = PEI.BuildingBlocks.MultiTenancy.ITenantContext;

namespace PEI.Identidad.Infrastructure.Tenancy;

public sealed class TenantContextAdapter(SharedTenantContext tenantContext) : ITenantContext
{
    public Guid? InstitucionId => tenantContext.InstitucionId;
}
