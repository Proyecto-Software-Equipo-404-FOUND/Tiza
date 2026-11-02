using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.Application.UseCases.Usuarios;

// S2-ID-10: lista los usuarios activos de la institución, opcionalmente filtrados por rol.
public sealed class ListarUsuariosHandler(IUsuariosConsultaDataAccess data)
{
    public Task<IReadOnlyList<UsuarioResumen>> HandleAsync(string? rol, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rol is not null && !IdentityRoles.IsCanonical(rol))
            throw new ValidationException("Rol invalido. Valores permitidos: Administrador, Docente o Alumno.");
        return data.ListarActivosAsync(rol, cancellationToken);
    }
}
