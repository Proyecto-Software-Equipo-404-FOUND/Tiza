using Microsoft.EntityFrameworkCore;
using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Domain.Entities;
using PEI.SalaEnVivo.Infrastructure.Persistence;

namespace PEI.SalaEnVivo.Infrastructure.Repositories
{
    public class AuditoriaFocoRepository : IAuditoriaFocoRepository
    {
        private readonly pei_sala_db _context;

        public AuditoriaFocoRepository(pei_sala_db context)
        {
            _context = context;
        }

        public async Task AddAsync(AuditoriaFoco evento, CancellationToken cancellationToken = default)
        {
            await _context.AuditoriaFoco.AddAsync(evento, cancellationToken);
        }

        public async Task<IReadOnlyList<AuditoriaFoco>> ObtenerPorSalaAsync(
            Guid salaId,
            CancellationToken cancellationToken = default)
        {
            return await _context.AuditoriaFoco
                .AsNoTracking()
                .Where(x => x.SalaId == salaId)
                .OrderBy(x => x.RegistradoUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AuditoriaFoco>> ObtenerPorAlumnoEnSalaAsync(
            Guid salaId,
            Guid alumnoId,
            CancellationToken cancellationToken = default)
        {
            return await _context.AuditoriaFoco
                .AsNoTracking()
                .Where(x => x.SalaId == salaId && x.AlumnoId == alumnoId)
                .OrderBy(x => x.RegistradoUtc)
                .ToListAsync(cancellationToken);
        }
    }
}
