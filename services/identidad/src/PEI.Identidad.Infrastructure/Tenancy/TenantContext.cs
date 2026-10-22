using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PEI.Identidad.Application.Abstractions;

namespace PEI.Identidad.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? InstitucionId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
                return null;

            var claims = user.FindAll("institucion_id").ToArray();
            if (claims.Length != 1)
                return null;
            var claim = claims[0].Value;

            return Guid.TryParse(claim, out var institucionId) && institucionId != Guid.Empty
                ? institucionId
                : null;
        }
    }
}
