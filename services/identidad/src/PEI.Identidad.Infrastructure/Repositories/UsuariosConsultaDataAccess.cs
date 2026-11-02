using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.UseCases.Usuarios;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.Infrastructure.Repositories;

// S2-ID-10. El filtro global de IdentidadDbContext (ID-05) limita los usuarios a la
// institución del token. No usar IgnoreQueryFilters: eso rompería el aislamiento.
public sealed class UsuariosConsultaDataAccess(IdentidadDbContext db) : IUsuariosConsultaDataAccess
{
    public async Task<IReadOnlyList<UsuarioResumen>> ListarActivosAsync(string? rol, CancellationToken cancellationToken = default)
    {
        var query = db.Usuarios.AsNoTracking().Where(u => u.Activo);
        if (rol is not null) query = query.Where(u => u.Rol == rol);
        return await query
            .OrderBy(u => u.Apellido).ThenBy(u => u.Nombre)
            .Select(u => new UsuarioResumen(u.Id, u.Nombre, u.Apellido, u.Email, u.Rol))
            .ToListAsync(cancellationToken);
    }

    public Task<UsuarioResumen?> ObtenerActivoAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Usuarios.AsNoTracking()
            .Where(u => u.Id == id && u.Activo)
            .Select(u => new UsuarioResumen(u.Id, u.Nombre, u.Apellido, u.Email, u.Rol))
            .FirstOrDefaultAsync(cancellationToken);
}
