using System.Collections.Generic;
using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.Interfaces.Repositories
{
    public interface IParticipanteSalaRepository
    {
        Task AddAsync(ParticipanteSala participante, CancellationToken cancellationToken);
        Task<bool> ExisteAlumnoEnSalaAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken);
        Task<IReadOnlyList<ParticipanteSala>> ObtenerPorSalaAsync(Guid salaId, CancellationToken cancellationToken = default);
        Task<Guid?> ObtenerUltimaSalaIdDeAlumnoAsync(Guid alumnoId, CancellationToken cancellationToken = default);
        Task<int> ContarAlumnosAsync(Guid salaId, CancellationToken cancellationToken = default);

        /// <summary>S3-SA-01 (#231): nombre de cada invitado de la sala, por su id (sub del token).</summary>
        Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresInvitadosAsync(Guid salaId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }
}
