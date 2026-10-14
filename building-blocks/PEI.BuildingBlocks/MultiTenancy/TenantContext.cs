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
        var user = context.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var institucionClaim = user.FindFirst("InstitucionId")?.Value
                                   ?? user.FindFirst("institucion_id")?.Value;
            if (Guid.TryParse(institucionClaim, out var institucionId))
            {
                tenantContext.InstitucionId = institucionId;
            }

            var usuarioClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? user.FindFirst("sub")?.Value;
            if (Guid.TryParse(usuarioClaim, out var usuarioId))
            {
                tenantContext.UsuarioId = usuarioId;
            }

            tenantContext.Rol = user.FindFirst(ClaimTypes.Role)?.Value
                                ?? user.FindFirst("role")?.Value;
        }

        await _next(context);
    }
}
