using services.SalaEnVivo.src.PEI.SalaEnVivo.Application.Interfaces.Repositories;
using services.SalaEnVivo.src.PEI.SalaEnVivo.Domain.Entities;
using services.SalaEnVivo.src.PEI.SalaEnVivo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace services.SalaEnVivo.src.PEI.SalaEnVivo.Infrastructure.Repositories
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
