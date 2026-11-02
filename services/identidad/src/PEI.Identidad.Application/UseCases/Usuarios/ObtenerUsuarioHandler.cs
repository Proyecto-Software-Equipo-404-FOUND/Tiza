using PEI.Identidad.Application.Abstractions;

namespace PEI.Identidad.Application.UseCases.Usuarios;

// S2-ID-10: devuelve un usuario activo de la institución, o null si no existe o es de otra institución.
public sealed class ObtenerUsuarioHandler(IUsuariosConsultaDataAccess data)
{
    public Task<UsuarioResumen?> HandleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id == Guid.Empty) return Task.FromResult<UsuarioResumen?>(null);
        return data.ObtenerActivoAsync(id, cancellationToken);
    }
}
