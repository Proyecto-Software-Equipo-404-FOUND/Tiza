    using PEI.SalaEnVivo.Application.Interfaces;

    namespace PEI.SalaEnVivo.Infrastructure.Persistence
{
        public class UnitOfWork : IUnitOfWork
        {
            private readonly pei_sala_db _context;

            public UnitOfWork(pei_sala_db context)
            {
                _context = context;
            }

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                return _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
