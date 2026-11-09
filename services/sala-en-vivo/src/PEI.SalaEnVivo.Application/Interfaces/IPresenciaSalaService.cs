using PEI.SalaEnVivo.Application.DTOs.Presencia;

namespace PEI.SalaEnVivo.Application.Interfaces
{
    public interface IPresenciaSalaService
    {
        Task<RegistroPresenciaDto> RegistrarConexionAsync(
            Guid salaId,
            Guid alumnoId,
            string connectionId,
            CancellationToken cancellationToken = default);

        Task<ResultadoDesconexionPresenciaDto> EliminarConexionAsync(
            Guid salaId,
            Guid alumnoId,
            string connectionId,
            CancellationToken cancellationToken = default);

        Task<PresenciaSalaDto> ObtenerPresenciaAsync(
            Guid salaId,
            CancellationToken cancellationToken = default);

        Task<bool> EsConexionActivaAsync(
            Guid salaId,
            Guid alumnoId,
            string connectionId,
            CancellationToken cancellationToken = default);
    }
}
