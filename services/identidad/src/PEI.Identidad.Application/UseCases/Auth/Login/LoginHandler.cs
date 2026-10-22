using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.Interfaces.Service;
using PEI.Identidad.Domain.Security;
using PEI.Identidad.Domain.Entities;
namespace PEI.Identidad.Application.UseCases.Auth.Login;
public sealed class LoginHandler(IAuthenticationDataAccess data, IPasswordHasher hasher, ITokenService tokens)
{
    public async Task<string> HandleAsync(LoginCommand command) => tokens.GenerateToken(await AuthenticateAsync(command));

    public async Task<Usuario> AuthenticateAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var usuario = await data.BuscarUsuarioParaLoginAsync(command.Email, cancellationToken);
        if (usuario is null)
        {
            hasher.VerifyDummy(command.Password);
            throw new UnauthorizedAccessException("Credenciales invalidas.");
        }
        // El acceso acotado garantiza actividad del usuario y de su institucion; no depende de navegaciones cargadas.
        var valido = hasher.Verify(command.Password, usuario.PasswordHash);
        if (!valido || !IdentityRoles.IsCanonical(usuario.Rol)) throw new UnauthorizedAccessException("Credenciales invalidas.");
        return usuario;
    }
}
