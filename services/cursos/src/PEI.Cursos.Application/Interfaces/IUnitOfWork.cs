using System.Threading;
using System.Threading.Tasks;

namespace PEI.Cursos.Application.Interfaces;

/* Abstraccion del patron Unit of Work.
Coordina la persistencia atomica de cambios en la base de datos PostgreSQL
asegurando la integridad transaccional de los casos de uso. */
public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
