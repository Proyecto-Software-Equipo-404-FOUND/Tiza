using services.SalaEnVivo.src.PEI.SalaEnVivo.Domain.Entities;

namespace services.SalaEnVivo.src.PEI.SalaEnVivo.Application.Interfaces.Repositories
{
    public interface ISalaRepository
    {
        Task AddAsync(Sala sala, CancellationToken cancellationToken);
        Task<bool> ExisteActivaPorPINAsync(int pin, CancellationToken cancellationToken);
    }
}
