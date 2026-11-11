using PEI.SalaEnVivo.Application.DTOs.Entregas;
using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;

namespace PEI.SalaEnVivo.Application.Interfaces
{
    public interface IEvaluacionesClient
    {
        Task<EvaluacionPreguntasDto?> ObtenerPreguntasAsync(
            Guid evaluacionId,
            Guid institucionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Despacha el lote de entregas de una sala finalizada hacia Evaluaciones
        /// (POST /interno/salas/{salaId}/entregas) según lote-entregas-v1.
        /// Reintenta hasta 3 veces ante 5xx o fallos de red. Retorna null ante 4xx o falla persistente.
        /// </summary>
        Task<ResultadoLoteEntregasDto?> EnviarLoteEntregasAsync(
            Guid salaId,
            LoteEntregasRequestDto lote,
            CancellationToken cancellationToken = default);
    }
}
