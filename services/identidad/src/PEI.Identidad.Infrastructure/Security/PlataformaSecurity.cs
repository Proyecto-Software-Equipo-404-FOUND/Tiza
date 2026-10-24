using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PEI.Identidad.Application.Plataforma;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Infrastructure.Security;

public sealed class PlataformaOptions
{
    public bool Enabled { get; set; }
    public JwtOptions Jwt { get; set; } = new();
    public string ActivationUrl { get; set; } = "";
    public string KeyDirectory { get; set; } = "";
    public string DevelopmentMailDirectory { get; set; } = "";
    public void Validate(JwtOptions institucional, bool development)
    {
        if (!Jwt.IsValid() || Jwt.SecretKey == institucional.SecretKey || Jwt.Issuer == institucional.Issuer || Jwt.Audience == institucional.Audience)
            throw new InvalidOperationException("JWT de plataforma requiere clave, issuer y audience propios y válidos.");
        if (!Uri.TryCreate(ActivationUrl, UriKind.Absolute, out var url) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) ||
            !string.IsNullOrEmpty(url.UserInfo) || (url.Scheme != "https" && !(development && url.Scheme == "http" && url.IsLoopback)))
            throw new InvalidOperationException("Configurar URL de activación HTTPS (HTTP solo en loopback de desarrollo).");
        if (!Path.IsPathFullyQualified(KeyDirectory)) throw new InvalidOperationException("Configurar directorio absoluto protegido de claves Data Protection.");
        if (development && !Path.IsPathFullyQualified(DevelopmentMailDirectory))
            throw new InvalidOperationException("Configurar directorio absoluto protegido del buzón de desarrollo.");
    }
}

public static class PlataformaJwtContract
{
    public static bool TryRead(ClaimsPrincipal principal, out Guid id)
    {
        id = Guid.Empty;
        if (principal.Identity?.IsAuthenticated != true || principal.Claims.Any(c =>
            c.Type is "institucion_id" or "InstitucionId" or "institucionId" or "UsuarioId" || c.Type == ClaimTypes.NameIdentifier || c.Type == ClaimTypes.Role)) return false;
        string? Single(string type)
        {
            var values = principal.FindAll(type).ToArray();
            return values.Length == 1 ? values[0].Value : null;
        }
        return Single("role") == "AdministradorGlobal" && Single("identity_type") == "platform" &&
            !new[] { "email", "jti", "iat" }.Any(t => principal.FindAll(t).Count() > 1) &&
            Guid.TryParse(Single("sub"), out id) && id != Guid.Empty;
    }
}

public sealed class PlataformaTokenService(PlataformaOptions options) : IPlataformaTokenService
{
    public string Emitir(CuentaPlataforma cuenta)
    {
        if (!cuenta.Activo || cuenta.Id == Guid.Empty || cuenta.Rol != "AdministradorGlobal") throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;
        var jwt = options.Jwt;
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(jwt.Issuer, jwt.Audience,
            new[] { new Claim("sub", cuenta.Id.ToString()), new Claim("role", "AdministradorGlobal"), new Claim("identity_type", "platform"),
                new Claim("jti", Guid.NewGuid().ToString()), new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) },
            now, now.AddMinutes(jwt.AccessTokenMinutes), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)), SecurityAlgorithms.HmacSha256)));
    }
}
