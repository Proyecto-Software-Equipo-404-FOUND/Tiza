using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Application.Interfaces;
using PEI.SalaEnVivo.Domain.Entities;
using PEI.SalaEnVivo.Infrastructure.Persistence;
using PEI.SalaEnVivo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace PEI.SalaEnVivo.Infrastructure.Repositories
{
    public class SalaRepository : ISalaRepository   
    {
        private readonly pei_sala_db _context;

        public SalaRepository(pei_sala_db context)
        {
            _context = context;
        }

        public async Task AddAsync(Sala sala, CancellationToken cancellationToken)
        {
            await _context.Sala.AddAsync(sala, cancellationToken);
        }

        public async Task<bool> ExisteActivaPorPINAsync(int pin, CancellationToken cancellationToken)
        {
            // Una sala en curso también ocupa su PIN: si no, una sala nueva podría repetirlo.
            return await _context.Sala.AnyAsync(s => s.CodigoPIN == pin && (s.Estado == "Activa" || s.Estado == "EnCurso"), cancellationToken);
        }

        public async Task<Sala?> ObtenerActivaPorPINAsync(int pin, CancellationToken cancellationToken)
        {
            return await _context.Sala.FirstOrDefaultAsync(
                    s => s.CodigoPIN == pin
                         // Incluye la sala en curso para que el alumno que cerró la pestaña pueda volver a entrar.
                         && (s.Estado == "Activa" || s.Estado == "EnCurso")
                         && s.FechaVencimiento > DateTime.UtcNow, cancellationToken);
        }

        public async Task<Sala?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Sala.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        }

        public async Task<ITransaccion> IniciarTransaccionAsync(CancellationToken cancellationToken = default)
        {
            var efTransaccion = await _context.Database.BeginTransactionAsync(cancellationToken);
            return new EfTransaccion(efTransaccion);
        }
    }
}
