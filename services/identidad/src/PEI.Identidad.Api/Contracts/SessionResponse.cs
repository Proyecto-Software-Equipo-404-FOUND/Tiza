using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.UseCases.Auth.Login;

namespace PEI.Identidad.Api.Contracts;

public sealed record SessionResponse(
    [property: Required] string AccessToken,
    [property: Required] string RefreshToken,
    [property: Required] string TokenType,
    [property: Required] DateTime AccessTokenExpiresAt,
    [property: Required] DateTime RefreshTokenExpiresAt)
{
    public static SessionResponse From(LoginResult session) => new(session.AccessToken, session.RefreshToken, "Bearer",
        session.AccessTokenExpiresAt, session.RefreshTokenExpiresAt);
}
