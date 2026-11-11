using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Correccion;

// S2-EV-02 (#144): acceso a datos para la corrección con IA.
public interface ICorreccionRepositorio
{
    // Entregas en PendienteCorreccion de todas las instituciones (las procesa el worker). Sin tracking.
    Task<List<EntregaExamen>> ListarPendientesAsync(int maximo, CancellationToken ct);

    // Entrega con tracking, para modificar su estado.
    Task<EntregaExamen?> ObtenerEntregaAsync(Guid entregaId, CancellationToken ct);

    Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct);

    Task<List<CalificacionSugerida>> ListarCalificacionesAsync(Guid entregaId, CancellationToken ct);

    Task AgregarCalificacionAsync(CalificacionSugerida calificacion, CancellationToken ct);

    // Lanza ConflictoConcurrenciaException si otra operación modificó la entrega antes.
    Task GuardarCambiosAsync(CancellationToken ct);
}
