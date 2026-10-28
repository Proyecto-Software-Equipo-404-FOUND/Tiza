using PEI.SalaEnVivo.Domain.Entities;

namespace PEI.SalaEnVivo.Application.Interfaces.Repositories
{
    public interface IRespuestaSalaRepository
    {
        Task<RespuestaSala?> ObtenerPorClaveIdempotenciaAsync(
            Guid salaId,
            Guid alumnoId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default);

        Task<bool> ExisteRespuestaAPreguntaAsync(
            Guid salaId,
            Guid alumnoId,
            Guid preguntaId,
            CancellationToken cancellationToken = default);

        Task<int> ObtenerUltimaSecuenciaAsync(
            Guid salaId,
            Guid alumnoId,
            CancellationToken cancellationToken = default);

        Task<List<Guid>> ObtenerPreguntasRespondidasAsync(
            Guid salaId,
            Guid alumnoId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            RespuestaSala respuesta,
            CancellationToken cancellationToken = default);
    }
}
