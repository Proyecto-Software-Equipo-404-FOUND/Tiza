using System.Text;
using Microsoft.IdentityModel.Tokens;
namespace PEI.Identidad.Infrastructure.Security;
public sealed class JwtOptions
{
    public string SecretKey { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int ClockSkewSeconds { get; set; } = 15;
    public bool IsValid() => !string.IsNullOrWhiteSpace(SecretKey) && Encoding.UTF8.GetByteCount(SecretKey) >= 32 &&
        !string.IsNullOrWhiteSpace(Issuer) && !string.IsNullOrWhiteSpace(Audience) &&
        AccessTokenMinutes is >= 1 and <= 60 && ClockSkewSeconds is >= 0 and <= 60;
    public TokenValidationParameters ValidationParameters()
    {
        if (!IsValid()) throw new InvalidOperationException("Configuracion JWT invalida: clave, issuer, audience o duracion.");
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromSeconds(ClockSkewSeconds),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    }
}
