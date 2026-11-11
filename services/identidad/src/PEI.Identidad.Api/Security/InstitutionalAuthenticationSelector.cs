using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PEI.Identidad.Api.Security;

public static class InstitutionalAuthenticationSelector
{
    public const string Scheme = "InstitutionalBearerSelector";
    public const string LocalDevelopmentScheme = "LocalDevelopment";

    public static AuthenticationBuilder AddInstitutionalAuthenticationSelector(
        this AuthenticationBuilder authentication,
        string localDevelopmentIssuer)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDevelopmentIssuer);

        return authentication.AddPolicyScheme(Scheme, displayName: null, options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var authorization = context.Request.Headers.Authorization.ToString();

                if (!AuthenticationHeaderValue.TryParse(authorization, out var header) ||
                    !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(header.Parameter))
                    return JwtBearerDefaults.AuthenticationScheme;

                var token = header.Parameter;
                var handler = new JwtSecurityTokenHandler();

                if (!handler.CanReadToken(token))
                    return JwtBearerDefaults.AuthenticationScheme;

                try
                {
                    var unvalidatedToken = handler.ReadJwtToken(token);
                    return string.Equals(unvalidatedToken.Issuer, localDevelopmentIssuer, StringComparison.Ordinal)
                        ? LocalDevelopmentScheme
                        : JwtBearerDefaults.AuthenticationScheme;
                }
                catch (ArgumentException)
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }
                catch (SecurityTokenException)
                {
                    return JwtBearerDefaults.AuthenticationScheme;
                }
            };
        });
    }
}
