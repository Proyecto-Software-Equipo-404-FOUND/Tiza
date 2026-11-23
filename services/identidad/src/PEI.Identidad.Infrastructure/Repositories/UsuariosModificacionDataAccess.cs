using Microsoft.EntityFrameworkCore;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Domain.Entities;
using PEI.Identidad.Infrastructure.Persistence;

namespace PEI.Identidad.Infrastructure.Repositories;

// S3-ID-01 (#243): Modificación de estado (baja/reactivación) de usuarios.
// No se usa IgnoreQueryFilters() para respetar el aislamiento institucional (ID-05).
public sealed class UsuariosModificacionDataAccess(IdentidadDbContext db) : IUsuariosModificacionDataAccess
{
    public Task<Usuario?> ObtenerUsuarioAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task DarDeBajaAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);
            if (usuario is not null && usuario.Activo)
            {
                usuario.Activo = false;
                await db.RefreshTokens
                    .Where(t => t.UsuarioId == usuarioId && !t.Revocado)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Revocado, true), cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            db.ChangeTracker.Clear();
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
