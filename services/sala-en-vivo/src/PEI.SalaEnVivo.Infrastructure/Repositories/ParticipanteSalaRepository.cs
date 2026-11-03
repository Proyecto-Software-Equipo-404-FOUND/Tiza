using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Domain.Entities;
using PEI.SalaEnVivo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PEI.SalaEnVivo.Infrastructure.Repositories
{

    public class ParticipanteSalaRepository : IParticipanteSalaRepository
    {
        private readonly pei_sala_db _context;

        public ParticipanteSalaRepository(pei_sala_db context)
        {
            _context = context;
        }

        public async Task AddAsync(ParticipanteSala participante, CancellationToken cancellationToken)
        {
            await _context.ParticipanteSala.AddAsync(participante, cancellationToken);
        }

        public async Task<bool> ExisteAlumnoEnSalaAsync(Guid salaId, Guid alumnoId, CancellationToken cancellationToken)
        {
            return await _context.ParticipanteSala.AnyAsync(
                p => p.SalaId == salaId && p.AlumnoId == alumnoId,
                cancellationToken);
        }

        public async Task<Guid?> ObtenerUltimaSalaIdDeAlumnoAsync(Guid alumnoId, CancellationToken cancellationToken = default)
        {
            return await _context.ParticipanteSala
                .Where(p => p.AlumnoId == alumnoId)
                .OrderByDescending(p => p.Id)
                .Select(p => (Guid?)p.SalaId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<int> ContarAlumnosAsync(Guid salaId, CancellationToken cancellationToken = default)
        {
            return _context.ParticipanteSala.CountAsync(p => p.SalaId == salaId, cancellationToken);
        }
    }
}
