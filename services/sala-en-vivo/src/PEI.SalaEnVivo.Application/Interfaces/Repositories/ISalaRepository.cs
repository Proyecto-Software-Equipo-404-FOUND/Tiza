using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.Interfaces.Repositories
{
    public interface ISalaRepository
    {
        Task AddAsync(Sala sala, CancellationToken cancellationToken);
        Task<bool> ExisteActivaPorPINAsync(int pin, CancellationToken cancellationToken);
    }
}
