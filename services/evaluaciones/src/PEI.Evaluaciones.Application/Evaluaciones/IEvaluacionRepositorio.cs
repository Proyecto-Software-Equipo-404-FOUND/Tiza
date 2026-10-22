using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Application.Evaluaciones;

public interface IEvaluacionRepositorio
{
    Task<Evaluacion?> ObtenerPorIdAsync(Guid id, Guid institucionId, CancellationToken ct);

    Task<List<Evaluacion>> ListarAsync(Guid institucionId, CancellationToken ct);

    Task AgregarAsync(Evaluacion evaluacion, CancellationToken ct);

    void Eliminar(Evaluacion evaluacion);

    Task GuardarCambiosAsync(CancellationToken ct);
}