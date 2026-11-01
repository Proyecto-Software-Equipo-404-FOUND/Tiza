using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.Interfaces.Repositories
{
    public interface IAuditoriaFocoRepository
    {
        Task AddAsync(AuditoriaFoco evento, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AuditoriaFoco>> ObtenerPorSalaAsync(Guid salaId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<AuditoriaFoco>> ObtenerPorAlumnoEnSalaAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken = default);
    }
}
