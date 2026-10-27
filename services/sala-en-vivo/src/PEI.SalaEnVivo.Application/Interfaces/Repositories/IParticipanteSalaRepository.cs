using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.Interfaces.Repositories
{
    public interface IParticipanteSalaRepository
    {
        Task AddAsync(ParticipanteSala participante, CancellationToken cancellationToken);
        Task<bool> ExisteAlumnoEnSalaAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken);
    }
}
