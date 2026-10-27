using System;

namespace PEI.SalaEnVivo.Application.Interfaces
{
    public interface ITransaccion : IDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
