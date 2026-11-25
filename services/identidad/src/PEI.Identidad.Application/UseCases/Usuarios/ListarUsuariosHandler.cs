using System.ComponentModel.DataAnnotations;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Security;

namespace PEI.Identidad.Application.UseCases.Usuarios;

// S2-ID-10: lista los usuarios activos de la institución, opcionalmente filtrados por rol.
public sealed class ListarUsuariosHandler(IUsuariosConsultaDataAccess data)
{
    public Task<IReadOnlyList<UsuarioResumen>> HandleAsync(string? rol, CancellationToken cancellationToken = default) =>
        HandleAsync(rol, incluirInactivos: false, cancellationToken);

    // S3-ID-01: con incluirInactivos también vienen los dados de baja (campo activo = false), para reactivarlos.
    public Task<IReadOnlyList<UsuarioResumen>> HandleAsync(string? rol, bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rol is not null && !IdentityRoles.IsCanonical(rol))
            throw new ValidationException("Rol invalido. Valores permitidos: Administrador, Docente o Alumno.");
        return incluirInactivos
            ? data.ListarIncluyendoInactivosAsync(rol, cancellationToken)
            : data.ListarActivosAsync(rol, cancellationToken);
    }
}
