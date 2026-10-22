using PEI.Identidad.Application.Interfaces.Repositories;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;

namespace PEI.Identidad.Application.UseCases.Auth.Login;

//El login comprueba las credenciales, verifica que el usuario y su institución estén activos y devuelve un JWT.

public class LoginHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginHandler(
        IUsuarioRepository usuarioRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<string> HandleAsync(LoginCommand command)
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

        return _tokenService.GenerateToken(usuario);
    }
}