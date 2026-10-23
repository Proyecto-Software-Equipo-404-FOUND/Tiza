namespace PEI.Evaluaciones.Application.Evaluaciones;

public interface ICursosClient
{
    Task<bool> ExisteCohorteAsync(Guid cohorteId, Guid institucionId, CancellationToken ct);
}