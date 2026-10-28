using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Domain.Entities;
using PEI.SalaEnVivo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PEI.SalaEnVivo.Infrastructure.Repositories
{
    public class RespuestaSalaRepository : IRespuestaSalaRepository
    {
        private readonly pei_sala_db _context;

        public RespuestaSalaRepository(pei_sala_db context)
        {
            _context = context;
        }

        public async Task<RespuestaSala?> ObtenerPorClaveIdempotenciaAsync(
            Guid salaId,
            Guid alumnoId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return await _context.RespuestasSala
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.SalaId == salaId &&
                         r.AlumnoId == alumnoId &&
                         r.IdempotencyKey == idempotencyKey,
                    cancellationToken);
        }

        public async Task<bool> ExisteRespuestaAPreguntaAsync(
            Guid salaId,
            Guid alumnoId,
            Guid preguntaId,
            CancellationToken cancellationToken = default)
        {
            return await _context.RespuestasSala
                .AnyAsync(
                    r => r.SalaId == salaId &&
                         r.AlumnoId == alumnoId &&
                         r.PreguntaId == preguntaId,
                    cancellationToken);
        }

        public async Task<int> ObtenerUltimaSecuenciaAsync(
            Guid salaId,
            Guid alumnoId,
            CancellationToken cancellationToken = default)
        {
            return await _context.RespuestasSala
                .Where(r => r.SalaId == salaId && r.AlumnoId == alumnoId)
                .Select(r => (int?)r.Secuencia)
                .MaxAsync(cancellationToken) ?? 0;
        }

        public async Task<List<Guid>> ObtenerPreguntasRespondidasAsync(
            Guid salaId,
            Guid alumnoId,
            CancellationToken cancellationToken = default)
        {
            return await _context.RespuestasSala
                .AsNoTracking()
                .Where(r => r.SalaId == salaId && r.AlumnoId == alumnoId)
                .Select(r => r.PreguntaId)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            RespuestaSala respuesta,
            CancellationToken cancellationToken = default)
        {
            await _context.RespuestasSala.AddAsync(respuesta, cancellationToken);
        }
    }
}
