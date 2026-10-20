using PEI.SalaEnVivo.Application.Interfaces.Repositories;
using PEI.SalaEnVivo.Domain.Entities;
using PEI.SalaEnVivo.Infrastructure.Persistence;
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
            return await _context.Sala.AnyAsync(s => s.CodigoPIN == pin && s.Estado == "Activa", cancellationToken);
        }
    }
}
