using PEI.SalaEnVivo.Application.DTOs.Evaluaciones;

namespace PEI.SalaEnVivo.Application.Interfaces
{
    public interface IEvaluacionesClient
    {
        Task<EvaluacionPreguntasDto?> ObtenerPreguntasAsync(
            Guid evaluacionId,
            Guid institucionId,
            CancellationToken cancellationToken = default);
    }
}
