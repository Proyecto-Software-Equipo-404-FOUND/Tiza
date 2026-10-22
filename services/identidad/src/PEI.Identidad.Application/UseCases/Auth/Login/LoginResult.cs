namespace PEI.Identidad.Application.UseCases.Auth.Login;

public record LoginResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);