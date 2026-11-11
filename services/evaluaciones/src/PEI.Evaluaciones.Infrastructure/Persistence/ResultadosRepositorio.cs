using Microsoft.EntityFrameworkCore;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Infrastructure.Persistence;

public class ResultadosRepositorio : IResultadosRepositorio
{
    private readonly EvaluacionDbContext _contexto;

    public ResultadosRepositorio(EvaluacionDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct)
    {
        return _contexto.Evaluaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == evaluacionId && e.InstitucionId == institucionId, ct);
    }

    public Task<List<EntregaExamen>> ListarEntregasPorEvaluacionAsync(
        Guid evaluacionId,
        Guid institucionId,
        CancellationToken ct)
    {
        return _contexto.EntregasExamen
            .AsNoTracking()
            .Where(e => e.EvaluacionId == evaluacionId && e.InstitucionId == institucionId)
            .OrderBy(e => e.FechaEntrega)
            .ToListAsync(ct);
    }

    public Task<EntregaExamen?> ObtenerEntregaPorIdAsync(
        Guid evaluacionId,
        Guid entregaId,
        Guid institucionId,
        CancellationToken ct)
    {
        return _contexto.EntregasExamen
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == entregaId && e.EvaluacionId == evaluacionId && e.InstitucionId == institucionId, ct);
    }

    public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregasAsync(
        List<Guid> entregaIds,
        CancellationToken ct)
    {
        return _contexto.CalificacionesSugeridas
            .AsNoTracking()
            .Where(c => entregaIds.Contains(c.EntregaId))
            .ToListAsync(ct);
    }

    public Task<List<CalificacionSugerida>> ListarCalificacionesPorEntregaAsync(
        Guid entregaId,
        CancellationToken ct)
    {
        return _contexto.CalificacionesSugeridas
            .AsNoTracking()
            .Where(c => c.EntregaId == entregaId)
            .ToListAsync(ct);
    }
}