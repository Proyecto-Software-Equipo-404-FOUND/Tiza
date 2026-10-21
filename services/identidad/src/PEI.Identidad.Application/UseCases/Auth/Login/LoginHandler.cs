
using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.Application.UseCases.Auth.Login;

public class LoginHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;

    public LoginHandler(
        IUsuarioRepository usuarioRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService,
        IRefreshTokenHasher refreshTokenHasher)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenService = refreshTokenService;
        _refreshTokenHasher = refreshTokenHasher;
    }

    public async Task<LoginResult> HandleAsync(LoginCommand command)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        var usuario = await _usuarioRepository.GetByEmailAsync(email);

        if (usuario is null ||
            !usuario.Activo ||
            !usuario.Institucion.Activo ||
            !_passwordHasher.Verify(command.Password, usuario.PasswordHash))
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        var accessToken = _tokenService.GenerateToken(usuario);
        var refreshToken = _refreshTokenService.GenerateToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshToken);

        var ahora = DateTime.UtcNow;
        var accessTokenExpiresAt = ahora.AddMinutes(60);
        var refreshTokenExpiresAt = ahora.AddDays(7);

        var entidad = new RefreshToken
        {
            UsuarioId = usuario.Id,
            Token = refreshTokenHash,
            FechaExpiracion = refreshTokenExpiresAt,
            Revocado = false
        };

        await _refreshTokenRepository.AddAsync(entidad);
        await _refreshTokenRepository.SaveChangesAsync();

        return new LoginResult(
            accessToken,
            refreshToken,
            accessTokenExpiresAt,
            refreshTokenExpiresAt);
    }
}
