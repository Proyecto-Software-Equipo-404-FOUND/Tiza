using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.Application.UseCases.Usuarios.Estado;

public sealed record ReactivarUsuarioCommand(Guid Id);

public sealed class ReactivarUsuarioHandler(IUsuariosModificacionDataAccess data)
{
    public async Task<ModificarEstadoUsuarioResultado> HandleAsync(ReactivarUsuarioCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (command.Id == Guid.Empty)
            return ModificarEstadoUsuarioResultado.NoEncontrado;

        var usuario = await data.ObtenerUsuarioAsync(command.Id, cancellationToken);
        if (usuario is null)
            return ModificarEstadoUsuarioResultado.NoEncontrado;

        if (!IdentityRoles.IsAssignable(usuario.Rol))
            return ModificarEstadoUsuarioResultado.RolNoPermitido;

        // Un pendiente de activación no tiene contraseña: reactivarlo lo dejaría activo sin poder entrar
        if (!usuario.Activo && usuario.PasswordHash == UsuarioPendiente.MarcaPasswordHash)
            return ModificarEstadoUsuarioResultado.PendienteActivacion;

        if (!usuario.Activo)
        {
            usuario.Activo = true;
            await data.SaveChangesAsync(cancellationToken);
        }

        return ModificarEstadoUsuarioResultado.Exitoso;
    }
}
