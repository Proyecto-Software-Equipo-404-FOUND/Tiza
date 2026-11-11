using Microsoft.EntityFrameworkCore;
using PEI.Evaluaciones.Application.Correccion;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Infrastructure.Persistence;

// S2-EV-02 (#144)
public sealed class CorreccionRepositorio : ICorreccionRepositorio
{
    private readonly EvaluacionDbContext _contexto;

    public CorreccionRepositorio(EvaluacionDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<List<EntregaExamen>> ListarPendientesAsync(int maximo, CancellationToken ct) =>
        _contexto.EntregasExamen
            .AsNoTracking()
            .Where(e => e.Estado == EstadoEntregaExamen.PendienteCorreccion)
            .OrderBy(e => e.FechaEntrega)
            .Take(maximo)
            .ToListAsync(ct);

    public Task<EntregaExamen?> ObtenerEntregaAsync(Guid entregaId, CancellationToken ct) =>
        _contexto.EntregasExamen.FirstOrDefaultAsync(e => e.Id == entregaId, ct);

    public Task<Evaluacion?> ObtenerEvaluacionAsync(Guid evaluacionId, Guid institucionId, CancellationToken ct) =>
        _contexto.Evaluaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == evaluacionId && e.InstitucionId == institucionId, ct);

    public Task<List<CalificacionSugerida>> ListarCalificacionesAsync(Guid entregaId, CancellationToken ct) =>
        _contexto.CalificacionesSugeridas
            .AsNoTracking()
            .Where(c => c.EntregaId == entregaId)
            .ToListAsync(ct);

    public async Task AgregarCalificacionAsync(CalificacionSugerida calificacion, CancellationToken ct) =>
        await _contexto.CalificacionesSugeridas.AddAsync(calificacion, ct);

    public async Task GuardarCambiosAsync(CancellationToken ct)
    {
        try
        {
            await _contexto.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries) entry.State = EntityState.Detached;
            throw new ConflictoConcurrenciaException(ex);
        }
    }
}
