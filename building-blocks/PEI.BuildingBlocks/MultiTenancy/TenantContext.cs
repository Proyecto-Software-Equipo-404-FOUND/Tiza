using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace PEI.BuildingBlocks.MultiTenancy;

public interface ITenantContext
{
    Guid? InstitucionId { get; }
    Guid? UsuarioId { get; }
    string? Rol { get; }
    bool EstaAutenticado { get; }
}

public class TenantContext : ITenantContext
{
    public Guid? InstitucionId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string? Rol { get; set; }
    public bool EstaAutenticado => UsuarioId.HasValue;
}

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        // Keep the context empty unless this request supplies valid claims. This
        // also prevents stale values if a context instance is accidentally reused.
        tenantContext.InstitucionId = null;
        tenantContext.UsuarioId = null;
        tenantContext.Rol = null;

        var user = context.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            tenantContext.InstitucionId = ReadGuidClaim(user, "institucion_id", "InstitucionId");
            tenantContext.UsuarioId = ReadGuidClaim(user, "sub", ClaimTypes.NameIdentifier);
            tenantContext.Rol = ReadClaim(user, "role", ClaimTypes.Role);
        }

        await _next(context);
    }

    private static Guid? ReadGuidClaim(ClaimsPrincipal user, string canonicalType, string aliasType)
    {
        var value = ReadClaim(user, canonicalType, aliasType);
        return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
    }

    private static string? ReadClaim(ClaimsPrincipal user, string canonicalType, string aliasType)
    {
        var canonicalClaims = user.FindAll(canonicalType).ToArray();
        var claims = canonicalClaims.Length == 0
            ? user.FindAll(aliasType).ToArray()
            : canonicalClaims;

        if (claims.Length != 1 || string.IsNullOrWhiteSpace(claims[0].Value))
        {
            return null;
        }

        return claims[0].Value;
    }
}
