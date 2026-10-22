using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using PEI.Identidad.Domain.Security;
namespace PEI.Identidad.Infrastructure.Security;
public static class JwtContract
{
    public static bool HasUniquePayloadKeys(SecurityToken token)
    {
        var encoded = token switch
        {
            JsonWebToken json => json.EncodedPayload,
            JwtSecurityToken jwt => jwt.RawPayload,
            _ => null
        };
        if (encoded is null) return false;
        try
        {
            using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(encoded));
            var names = payload.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
            return names.Length == names.Distinct(StringComparer.Ordinal).Count();
        }
        catch (Exception e) when (e is JsonException or FormatException) { return false; }
    }
    public static bool TryRead(ClaimsPrincipal principal, out Guid usuarioId, out Guid institucionId, out string role)
    {
        usuarioId = institucionId = Guid.Empty; role = "";
        string? Single(string type)
        {
            var claims = principal.Claims.Where(c => c.Type == type).ToArray();
            return claims.Length == 1 ? claims[0].Value : null;
        }
        if (principal.Claims.Any(c => c.Type is "InstitucionId" or "institucionId" or "UsuarioId" || c.Type == ClaimTypes.NameIdentifier || c.Type == ClaimTypes.Role)) return false;
        if (new[] { "email", "jti", "iat" }.Any(t => principal.Claims.Count(c => c.Type == t) > 1)) return false;
        role = Single("role") ?? "";
        return Guid.TryParse(Single("sub"), out usuarioId) && usuarioId != Guid.Empty &&
            Guid.TryParse(Single("institucion_id"), out institucionId) && institucionId != Guid.Empty && IdentityRoles.IsCanonical(role);
    }
}
