namespace PEI.SalaEnVivo.Application.Interfaces
{
    public interface ISalaCronometroService
    {
        Task EstablecerVencimientoAsync(
            Guid salaId,
            DateTime fechaVencimientoUtc,
            CancellationToken cancellationToken = default);

        Task<DateTime?> ObtenerVencimientoAsync(
            Guid salaId,
            CancellationToken cancellationToken = default);

        Task<bool> EstaVencidaAsync(
            Guid salaId,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);
    }
}
