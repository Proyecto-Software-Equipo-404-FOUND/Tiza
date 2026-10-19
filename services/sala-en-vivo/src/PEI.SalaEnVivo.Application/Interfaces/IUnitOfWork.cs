namespace services.SalaEnVivo.src.PEI.SalaEnVivo.Application.Interfaces
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
