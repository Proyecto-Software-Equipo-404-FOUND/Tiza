using Microsoft.EntityFrameworkCore;
using PEI.Evaluaciones.Application.Evaluaciones;
using PEI.Evaluaciones.Domain.Entities;

namespace PEI.Evaluaciones.Infrastructure.Persistence;

public class EvaluacionRepositorio : IEvaluacionRepositorio
{
    private readonly EvaluacionDbContext _contexto;

    public EvaluacionRepositorio(EvaluacionDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<Evaluacion?> ObtenerPorIdAsync(Guid id, Guid institucionId, CancellationToken ct)
    {
        return _contexto.Evaluaciones
            .FirstOrDefaultAsync(e => e.Id == id && e.InstitucionId == institucionId, ct);
    }

    public Task<List<Evaluacion>> ListarAsync(Guid institucionId, CancellationToken ct)
    {
        return _contexto.Evaluaciones
            .AsNoTracking()
            .Where(e => e.InstitucionId == institucionId)
            .OrderByDescending(e => e.FechaCreacion)
            .ToListAsync(ct);
    }

    public async Task AgregarAsync(Evaluacion evaluacion, CancellationToken ct)
    {
        await _contexto.Evaluaciones.AddAsync(evaluacion, ct);
    }

    public void Eliminar(Evaluacion evaluacion)
    {
        _contexto.Evaluaciones.Remove(evaluacion);
    }

    public Task GuardarCambiosAsync(CancellationToken ct)
    {
        return _contexto.SaveChangesAsync(ct);
    }
}