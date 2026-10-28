using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.Interfaces.Repositories
{
    public interface ISalaRepository
    {
        Task AddAsync(Sala sala, CancellationToken cancellationToken);
        Task<bool> ExisteActivaPorPINAsync(int pin, CancellationToken cancellationToken);
        Task<Sala?> ObtenerActivaPorPINAsync(int pin, CancellationToken cancellationToken);
        Task<Sala?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default);
    }
}
