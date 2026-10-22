using System.Threading;
using System.Threading.Tasks;
using PEI.Cursos.Application.Interfaces;
using PEI.Cursos.Infrastructure.Persistence;

namespace PEI.Cursos.Infrastructure.Persistence;

/* Implementacion concreta del patron Unit of Work sobre el DbContext de EF Core.
Concentra y confirma en una unica transaccion atonica todos los cambios acumulados por los repositorios. */
public class UnitOfWork : IUnitOfWork
{
    private readonly CursosDbContext _context;

    public UnitOfWork(CursosDbContext context)
    {
        _context = context;
    }

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
