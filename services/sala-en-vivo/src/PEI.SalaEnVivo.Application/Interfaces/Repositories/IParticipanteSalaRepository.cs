using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.Interfaces.Repositories
{
    public interface IParticipanteSalaRepository
    {
        Task AddAsync(ParticipanteSala participante, CancellationToken cancellationToken);
        Task<bool> ExisteAlumnoEnSalaAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken);
        Task<Guid?> ObtenerUltimaSalaIdDeAlumnoAsync(Guid alumnoId, CancellationToken cancellationToken = default);
        Task<int> ContarAlumnosAsync(Guid salaId, CancellationToken cancellationToken = default);
    }
}
