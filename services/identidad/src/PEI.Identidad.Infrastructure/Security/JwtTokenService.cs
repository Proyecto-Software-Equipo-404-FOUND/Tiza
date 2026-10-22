using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Domain.Security;
namespace PEI.Identidad.Infrastructure.Security;
public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService, IAccessTokenIssuer
{
    public string GenerateToken(Usuario usuario) => Issue(usuario).AccessToken;

    public IssuedAccessToken Issue(Usuario usuario)
    {
        var config = options.Value;
        if (!config.IsValid()) throw new InvalidOperationException("Configuracion JWT invalida.");
        if (usuario.Id == Guid.Empty || usuario.InstitucionId == Guid.Empty || !IdentityRoles.IsCanonical(usuario.Rol))
            throw new ArgumentException("Identidad no valida para emision.");
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
        var claims = new List<Claim>
        {
            new("sub", usuario.Id.ToString()), new("institucion_id", usuario.InstitucionId.ToString()), new("role", usuario.Rol),
            new("jti", Guid.NewGuid().ToString()), new("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        if (!string.IsNullOrWhiteSpace(usuario.Email)) claims.Add(new("email", usuario.Email));
        var expires = now.AddMinutes(config.AccessTokenMinutes);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(config.Issuer, config.Audience, claims,
            now, expires, new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.SecretKey)), SecurityAlgorithms.HmacSha256)));
        return new(token, expires);
    }
}
