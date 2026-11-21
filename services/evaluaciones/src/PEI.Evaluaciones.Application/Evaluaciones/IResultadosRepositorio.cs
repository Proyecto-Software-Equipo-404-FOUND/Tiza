using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public interface IResultadosRepositorio
{
    // Métodos previos existentes (En ConsultarResultados.cs)
    Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct);
    Task<List<EntregaExamen>> ListarEntregasPorEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct);
    Task<EntregaExamen?> ObtenerEntregaPorIdAsync(Guid evaluacionId, Guid entregaId, Guid institucionId, CancellationToken ct);
    Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregasAsync(List<Guid> entregaIds, CancellationToken ct);
    Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregaAsync(Guid entregaId, CancellationToken ct);

    // S3-EV-03 (#224): Métodos nuevos para CalificacionDocente
    Task<CalificacionDocente?> ObtenerCalificacionDocenteAsync(Guid entregaId, Guid preguntaId, CancellationToken ct);
    Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregaAsync(Guid entregaId, CancellationToken ct);
    Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregasAsync(List<Guid> entregaIds, CancellationToken ct);
    Task GuardarCalificacionDocenteAsync(CalificacionDocente calificacion, CancellationToken ct);
    Task GuardarCambiosAsync(CancellationToken ct);
}