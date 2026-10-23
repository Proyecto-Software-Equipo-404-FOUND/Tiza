using PEI.SalaEnVivo.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace PEI.SalaEnVivo.Infrastructure.Persistence
{
    public class EfTransaccion : ITransaccion
    {
        private readonly IDbContextTransaction _transaccion;

        public EfTransaccion(IDbContextTransaction transaccion)
        {
            _transaccion = transaccion;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
           => _transaccion.CommitAsync(cancellationToken);
        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => _transaccion.RollbackAsync(cancellationToken);

        public void Dispose()
            => _transaccion.Dispose();
    }
}
