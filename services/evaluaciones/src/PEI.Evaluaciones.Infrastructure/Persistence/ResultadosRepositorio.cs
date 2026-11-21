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

    public Task<CalificacionDocente?> ObtenerCalificacionDocenteAsync(
        Guid entregaId,
        Guid preguntaId,
        CancellationToken ct)
    {
        return _contexto.CalificacionesDocente
            .FirstOrDefaultAsync(c => c.EntregaId == entregaId && c.PreguntaId == preguntaId, ct);
    }

    public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregaAsync(
        Guid entregaId,
        CancellationToken ct)
    {
        return _contexto.CalificacionesDocente
            .AsNoTracking()
            .Where(c => c.EntregaId == entregaId)
            .ToListAsync(ct);
    }

    public Task<List<CalificacionDocente>> ListarCalificacionesDocentePorEntregasAsync(
        List<Guid> entregaIds,
        CancellationToken ct)
    {
        return _contexto.CalificacionesDocente
            .AsNoTracking()
            .Where(c => entregaIds.Contains(c.EntregaId))
            .ToListAsync(ct);
    }

    public async Task GuardarCalificacionDocenteAsync(CalificacionDocente calificacion, CancellationToken ct)
    {
        var existente = await _contexto.CalificacionesDocente
            .FirstOrDefaultAsync(c => c.EntregaId == calificacion.EntregaId && c.PreguntaId == calificacion.PreguntaId, ct);

        if (existente is null)
        {
            await _contexto.CalificacionesDocente.AddAsync(calificacion, ct);
        }
        else
        {
            existente.PuntajeDocente = calificacion.PuntajeDocente;
            existente.ComentarioDocente = calificacion.ComentarioDocente;
            existente.DesgloseJson = calificacion.DesgloseJson;
            existente.FechaEdicionUtc = calificacion.FechaEdicionUtc;
        }

        await _contexto.SaveChangesAsync(ct);
    }

    public Task GuardarCambiosAsync(CancellationToken ct) => _contexto.SaveChangesAsync(ct);

    public async Task<List<(EntregaExamen Entrega, Evaluacion Evaluacion)>> ListarEntregasPorAlumnoAsync(Guid alumnoId, CancellationToken ct)
    {
        var filas = await (
            from entrega in _contexto.EntregasExamen
            join evaluacion in _contexto.Evaluaciones on entrega.EvaluacionId equals evaluacion.Id
            where entrega.AlumnoId == alumnoId
            orderby entrega.FechaEntrega descending
            select new { entrega, evaluacion }
        ).AsNoTracking().ToListAsync(ct);

        return filas.Select(x => (x.entrega, x.evaluacion)).ToList();
    }
}